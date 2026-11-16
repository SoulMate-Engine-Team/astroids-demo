using SoulMate;
using SoulMate.Logging;

public class Asteroids : GameLogic
{
	public override void Start()
	{
		Logger.Log("Started");
	}

	public override void Terminate()
	{
		Logger.Log("Terminated");
	}

	public override void Update()
	{
		Logger.Log("Update");
	}
}