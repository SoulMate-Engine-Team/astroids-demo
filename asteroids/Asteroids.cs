using Silk.NET.Input;
using SoulMate;
using SoulMate.Logging;
using SoulMate.Modules;
using SoulMate.Modules.Audio;

namespace AsteroidsGame;

public sealed class Asteroids : GameLogic
{
	private AudioBuffer fireBuffer;
	private AudioEngine engine;

	public override void Start()
	{
		engine = AudioModule.Instance.CreateEngine();
		var path = Path.Combine(AppContext.BaseDirectory, "../../../resources/fire.wav");
		fireBuffer = engine.LoadFileToBuffer(path);
	}

	public override void Terminate()
	{
		
	}

	public override void Update()
	{
		engine.DrainOneShotQueue();

		if (InputModule.Instance.WasPressedThisFrame(Key.Space))
		{
			engine.PlayOneShot(fireBuffer);
		}
	}
}
