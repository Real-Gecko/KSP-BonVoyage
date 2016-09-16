using System;

namespace BonVoyage
{
	public class ActiveRover
	{
		public string name;
		public string bodyName;
		public string status;
		public double toTravel;
		public MapObject mapObject;
		public double averageSpeed;
		public ActiveRover (string name, string bodyName, string status, double toTravel, MapObject mapObject)
		{
			this.name = name;
			this.bodyName = bodyName;
			this.status = status;
			this.toTravel = toTravel;
			this.mapObject = mapObject;
		}
	}
}

