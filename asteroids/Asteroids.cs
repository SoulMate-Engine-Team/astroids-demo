using System.Diagnostics;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SoulMate;
using SoulMate.Graphics;
using SoulMate.Graphics.Resources.Global;
using SoulMate.Logging;
using SoulMate.Mathematics;
using SoulMate.Modules;
using SoulMate.Modules.Audio;

namespace AsteroidsGame;

sealed class Asteroid(Sprite sprite, int frame)
{
	public Sprite Sprite { get; init; } = sprite;
	public int Frame { get; init; } = frame;
}

public sealed class Asteroids : GameLogic
{
	private static readonly Random rnd = new();
	private AudioBuffer fireBuffer;
	private AudioBuffer explodeBuffer;
	private AudioEngine engine;
	private Texture2D shipSpriteSheet;
	private TextureAtlas asteroidAtlas;
	private Texture2D explosionParticleSprite;
	private Texture2D bulletSprite;

	private Sprite ship;

	private readonly Stopwatch frameTimer = Stopwatch.StartNew();
	private readonly List<Sprite> bullets = [];
	private readonly List<Asteroid> asteroids = [];

	private const float maxAsteroidCount = 12;
	private const float asteroidSpeed = 150;
	private const float rotationSpeed = 5;
	private const float shipAcceleration = 500;
	private const float friction = 100;
	private const float maxShipSpeed = 600;
	private const float bulletSpeed = 1000;
	private const float bulletCooldownMax = 0.15F;
	private float bulletCooldown = bulletCooldownMax;
	private Vector2 shipVelocity;
	private int points = 0;

	public static Vector2 RotateAround(Vector2 vec, float angleRad, Vector2 center)
	{
		float c = MathF.Cos(angleRad);
		float s = MathF.Sin(angleRad);

		Vector2 d = vec - center;
		
		return new Vector2(
			d.X * c - d.Y * s + center.X,
			d.X * s + d.Y * c + center.Y);
	}

	public override void Start()
	{
		WindowManager.GameWindow!.Center();
		GraphicsEnvironment.GetAPI().EnableCapability(EnableCap.Blend);

		var basePath = Path.Combine(AppContext.BaseDirectory, "../../../resources");

		using (var log = new ScopedLog("init audio engine"))
		{
			engine = AudioModule.Instance.CreateEngine();
		}

		using (var log = new ScopedLog("load sfx"))
		{
			var fireSfxPath = Path.Combine(basePath, "fire.wav");
			var explodeSfxPath = Path.Combine(basePath, "explode.wav");
			fireBuffer = engine.LoadFileToBuffer(fireSfxPath);
			explodeBuffer = engine.LoadFileToBuffer(explodeSfxPath);
		}

		using (var log = new ScopedLog("load sprites"))
		{
			shipSpriteSheet = Texture2D.FromFile(Path.Combine(basePath, "spaceship_spritesheet.png"));
			var asteroidSpriteSheet = Texture2D.FromFile(Path.Combine(basePath, "asteroids_spritesheet.png"));
			asteroidAtlas = new(asteroidSpriteSheet, new(82, 75));
			explosionParticleSprite = Texture2D.FromFile(Path.Combine(basePath, "explosion_particle.png"));
			bulletSprite = Texture2D.FromFile(Path.Combine(basePath, "bullet.png"));
		}

		foreach (var res in new[] { shipSpriteSheet, asteroidAtlas.Texture, explosionParticleSprite, bulletSprite })
		{
			res.MagFilter = TextureMagFilter.Nearest;
			res.MinFilter = TextureMinFilter.Nearest;
		}

		engine.Volume = 0.1F;

		ship = new Sprite(shipSpriteSheet)
		{
			Position = new(500, 400),
			Rotation = MathF.PI / 4,
			Size = new Vector2(16, 27) * 3
		};

		RendererModule.Drawables.Add(ship);
		frameTimer.Start();
	}

	public override void Terminate()
	{
		fireBuffer.Dispose();
		engine.Dispose();
	}

	public override void Update()
	{
		engine.DrainOneShotQueue();

		var dt = (float)frameTimer.Elapsed.TotalSeconds;
		frameTimer.Restart();
		bulletCooldown = MathF.Max(bulletCooldown - dt, 0F);

		var inputModule = InputModule.Instance;
		var wnd = WindowManager.GameWindow!;

		bool thrusting = inputModule.IsKeyPressed(Key.W) || inputModule.IsKeyPressed(Key.Up);
		Vector2 forward = RotateAround(-Vector2.UnitY, ship.Rotation, Vector2.Zero);

		if (thrusting)
		{
			shipVelocity += forward * (shipAcceleration * dt);
		}

		float speed = shipVelocity.Length;
		speed = MathF.Max(0f, speed - friction * dt);
		speed = MathF.Min(speed, maxShipSpeed);
		shipVelocity = shipVelocity.Normalized * speed;

		ship.Position += shipVelocity * dt;

		ship.SetFrame(thrusting ? 16 : 0, 0, 16, 27);

		WrapSpritePosition(ship);

		if (inputModule.IsKeyPressed(Key.A) || inputModule.IsKeyPressed(Key.Left))
		{
			ship.Rotation -= rotationSpeed * dt;
		}
		if (inputModule.IsKeyPressed(Key.D) || inputModule.IsKeyPressed(Key.Right))
		{
			ship.Rotation += rotationSpeed * dt;
		}

		if (inputModule.IsKeyPressed(Key.Space) && bulletCooldown <= 0F)
		{
			bulletCooldown = bulletCooldownMax;
			Sprite bullet = new(bulletSprite)
			{
				Position = ship.Position + RotateAround(ship.Size.Length * -Vector2.UnitY / 2, ship.Rotation, Vector2.Zero),
				Rotation = ship.Rotation
			};
			bullet.Size *= 3;
			bullets.Add(bullet);
			RendererModule.Drawables.Add(bullet);
			engine.PlayOneShot(fireBuffer);
		}

		if (asteroids.Count < maxAsteroidCount && asteroids.Count < 1)
		{
			var radius = RandomRadiusInAnnulus(ship.Size.Length * 2F, ship.Size.Length * 2.5F);
			var angle = rnd.NextSingle() * MathF.Tau;

			var offset = radius * RotateAround(Vector2.UnitY, angle, Vector2.Zero);
			var frame = rnd.Next(0, 2);
			Sprite asteroid = asteroidAtlas[frame, 0];
			asteroid.Position = ship.Position + offset;
			asteroid.Rotation = angle;
			
			asteroids.Add(new(asteroid, frame));
			RendererModule.Drawables.Add(asteroid);
		}

		try
		{
			while (bullets.First(
				bullet => {
					return bullet.Position.X < -bullet.Size.X || 
					bullet.Position.Y < -bullet.Size.Y || 
					bullet.Position.X > bullet.Size.X + wnd.Size.X || 
					bullet.Position.Y > bullet.Size.Y + wnd.Size.Y;
				}) is { } bullet)
			{
				bullets.Remove(bullet);
				RendererModule.Drawables.Remove(bullet);
			}
		}
		catch (InvalidOperationException) { }

		List<Asteroid> newAsteroids = [];
		List<Sprite> bulletRemove = [];
		foreach (var bullet in bullets)
		{
			bullet.Position += bulletSpeed * dt * RotateAround(-Vector2.UnitY, bullet.Rotation, Vector2.Zero);
			Asteroid? colliding = null;
			foreach (var asteroid in asteroids)
			{
				if (SpriteIntersecting(bullet, asteroid.Sprite))
				{
					colliding = asteroid;
					break;
				}
			}
			if (colliding is not { } col)
			{
				continue;
			}
			engine.PlayOneShot(explodeBuffer, new AudioOneShotSettings() with { Volume = 0.15F });
			if (col.Frame == asteroidAtlas.TileCount - 1)
			{
				asteroids.Remove(colliding);
				RendererModule.Drawables.Remove(colliding.Sprite);
				continue;
			}

			RendererModule.Drawables.Remove(colliding.Sprite);
			asteroids.Remove(colliding);

			var angle1 = colliding.Sprite.Rotation + MathF.PI / 2;
			var pos1 = colliding.Sprite.Position + RotateAround(Vector2.UnitY, angle1, Vector2.Zero);
			var angle2 = colliding.Sprite.Rotation - MathF.PI / 2;
			var pos2 = colliding.Sprite.Position + RotateAround(Vector2.UnitY, angle2, Vector2.Zero);

			var frame = colliding.Frame + 1;
			Sprite asteroid1 = asteroidAtlas[frame, 0];
			asteroid1.Position = pos1;
			asteroid1.Rotation = angle1;
			
			newAsteroids.Add(new(asteroid1, frame));

			Sprite asteroid2 = asteroidAtlas[frame, 0];
			asteroid2.Position = pos2;
			asteroid2.Rotation = angle2;
			
			newAsteroids.Add(new(asteroid2, frame));
			bulletRemove.Add(bullet);
		}

		foreach (var asteroid in newAsteroids)
		{
			asteroids.Add(asteroid);
			RendererModule.Drawables.Add(asteroid.Sprite);
		}
		foreach (var bullet in bulletRemove)
		{
			bullets.Remove(bullet);
			RendererModule.Drawables.Remove(bullet);
		}

		foreach (var asteroid in asteroids)
		{
			asteroid.Sprite.Position += 
				asteroidSpeed * dt * 
				MathF.Sqrt(asteroid.Frame + 1) * 
				RotateAround(-Vector2.UnitY, asteroid.Sprite.Rotation, Vector2.Zero);
			WrapSpritePosition(asteroid.Sprite);
		}

		wnd.Title = $"Points {points}";
	}

	private static float RandomRadiusInAnnulus(float rMin, float rMax)
	{
		if (rMin >= rMax)
		{
			throw new ArgumentException($"rMin({rMin}) must be >= rMax({rMax})");
		}

		var num = rnd.NextSingle();
		return rMin + num * (rMax - rMin);
	}

	private static void WrapSpritePosition(Sprite sprite)
	{
		var wnd = WindowManager.GameWindow!;

		if (sprite.Position.X < 0)
		{
			sprite.Position.X = wnd.Size.X;
		}
		else if (sprite.Position.X > wnd.Size.X)
		{
			sprite.Position.X = 0;
		}
		if (sprite.Position.Y < 0)
		{
			sprite.Position.Y = wnd.Size.Y;
		}
		else if (sprite.Position.Y > wnd.Size.Y)
		{
			sprite.Position.Y = 0;
		}
	}

	private static bool SpriteIntersecting(Sprite a, Sprite b)
	{
		var posA = a.Position;
		var sizeHA = a.Size / 2;
		var posB = b.Position;
		var sizeHB = b.Size / 2;

		var aLeft = posA.X - sizeHA.X;
		var aRight = posA.X + sizeHA.X;
		var aTop = posA.Y - sizeHA.Y;
		var aBottom = posA.Y + sizeHA.Y;
		
		var bLeft = posB.X - sizeHB.X;
		var bRight = posB.X + sizeHB.X;
		var bTop = posB.Y - sizeHB.Y;
		var bBottom = posB.Y + sizeHB.Y;

		return !(
			aLeft > bRight ||
			aRight < bLeft ||
			aBottom < bTop ||
			aTop > bBottom);
	}
}
