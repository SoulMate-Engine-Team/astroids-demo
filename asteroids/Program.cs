using SoulMate;
using SoulMate.Modules;
using AsteroidsGame;
using Silk.NET.Windowing;

ModuleManager.UseDefaults();
EngineCore.Run(new Asteroids(), WindowOptions.Default with
{
	Size = new(1000, 800), 
	WindowBorder = WindowBorder.Fixed
});