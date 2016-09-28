using System;
namespace BonVoyage
{
	public class GeoUtils
	{
		private const double PI = Math.PI;

		// Haversine algorithm
		// http://www.movable-type.co.uk/scripts/latlong.html
		internal static double GetDistance(double startLatitude, double startLongitude, double endLatitude, double endLongitude, double radius)
		{
			double deltaLatitude =  PI/ 180 * (endLatitude - startLatitude);
			double deltaLongitude = PI/ 180 * (endLongitude - startLongitude);

			startLatitude = PI/ 180 * startLatitude;
			startLongitude = PI/ 180 * startLongitude;
			endLatitude = PI/ 180 * endLatitude;
			endLongitude = PI/ 180 * endLongitude;

			double a = Math.Pow(Math.Sin(deltaLatitude / 2), 2) + Math.Cos(startLatitude) * Math.Cos(endLatitude) *
					   Math.Pow(Math.Sin(deltaLongitude / 2), 2);

			double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

			double distance = radius * c;
			return distance;
		}

		// Rad version, useless?
		internal static double GetDistanceRad(double startLatitude, double startLongitude, double endLatitude, double endLongitude, double radius)
		{
			double deltaLatitude =  endLatitude - startLatitude;
			double deltaLongitude = endLongitude - startLongitude;

			double a = Math.Pow(Math.Sin(deltaLatitude / 2), 2) + Math.Cos(startLatitude) * Math.Cos(endLatitude) *
				Math.Pow(Math.Sin(deltaLongitude / 2), 2);

			double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

			double distance = radius * c;
			return distance;
		}

		// Alternative haversine distance implementation
		// https://www.kaggle.com/c/santas-stolen-sleigh/forums/t/18049/simpler-faster-haversine-distance
/*		public static double GetDistanceAlt()
		{
		}
*/

		// Bearing from start to end
		// http://www.movable-type.co.uk/scripts/latlong.html
		internal static double InitialBearing(double startLatitude, double startLongitude, double targetLatitude, double targetLongitude)
		{
			startLatitude = PI/ 180 * startLatitude;
			startLongitude = PI/ 180 * startLongitude;
			targetLatitude = PI/ 180 * targetLatitude;
			targetLongitude = PI/ 180 * targetLongitude;

			double y = Math.Sin (targetLongitude - startLongitude) * Math.Cos (targetLatitude);
			double x = Math.Cos (startLatitude) * Math.Sin (targetLatitude) -
				Math.Sin (startLatitude) * Math.Cos (targetLatitude) * Math.Cos (targetLongitude - startLongitude);

			double bearing = Math.Atan2 (y, x);
			bearing = (bearing * 180.0 / PI + 360) % 360;

			return bearing;
		}
			
		// Bearing from start to end, rad version
		// http://www.movable-type.co.uk/scripts/latlong.html
		internal static double InitialBearingRad(double startLatitude, double startLongitude, double targetLatitude, double targetLongitude)
		{
			double y = Math.Sin (targetLongitude - startLongitude) * Math.Cos (targetLatitude);
			double x = Math.Cos (startLatitude) * Math.Sin (targetLatitude) -
				Math.Sin (startLatitude) * Math.Cos (targetLatitude) * Math.Cos (targetLongitude - startLongitude);

			double bearing = Math.Atan2 (y, x);
			return bearing;
		}

		// Bearing at destination
		// http://www.movable-type.co.uk/scripts/latlong.html
		internal static double FinalBearing(double startLatitude, double startLongitude, double targetLatitude, double targetLongitude) {
			double bearing = InitialBearing (targetLatitude, targetLongitude, startLatitude, startLongitude);
			bearing = (bearing + 180) % 360;
			return bearing;
		}

		// Bearing at destination, rad version
		// http://www.movable-type.co.uk/scripts/latlong.html
		internal static double FinalBearingRad(double startLatitude, double startLongitude, double targetLatitude, double targetLongitude) {
			double bearing = InitialBearingRad (targetLatitude, targetLongitude, startLatitude, startLongitude);
//			bearing = (bearing + 180) % 360;
			return bearing;
		}

		// "Reverse Haversine" Formula
		// https://gist.github.com/shayanjm/644d895c1fad80b49919
		internal static double[] GetLatitudeLongitude(double latStart, double lonStart, double bearing, double distance, double radius)
		{
			latStart = PI/ 180 * latStart;
			lonStart = PI/ 180 * lonStart;
			bearing = PI/ 180 * bearing;

			var latEnd = Math.Asin(Math.Sin(latStart) * Math.Cos(distance / radius) +
								   Math.Cos(latStart) * Math.Sin(distance / radius) * Math.Cos(bearing));
			var lonEnd = lonStart + Math.Atan2(Math.Sin(bearing) * Math.Sin(distance / radius) * Math.Cos(latStart),
												 Math.Cos(distance / radius) - Math.Sin(latStart) * Math.Sin(latEnd));

			return new double[] {
				latEnd * 180.0 / PI,
				lonEnd * 180.0 / PI
			};
		}

		// Get altitude at point. Cinically stolen from Waypoint Manager source code :D
		internal static double TerrainHeightAt(double latitude, double longitude, CelestialBody body)
		{
			// Not sure when this happens - for Sun and Jool?
			if (body.pqsController == null)
			{
				return 0;
			}

			// Figure out the terrain height
			double latRads = PI / 180.0 * latitude;
			double lonRads = PI / 180.0 * longitude;
			Vector3d radialVector = new Vector3d(Math.Cos(latRads) * Math.Cos(lonRads), Math.Sin(latRads), Math.Cos(latRads) * Math.Sin(lonRads));
			return body.pqsController.GetSurfaceHeight (radialVector) - body.pqsController.radius;
//			return Math.Max(body.pqsController.GetSurfaceHeight(radialVector) - body.pqsController.radius, 0.0);
		}
	}
}
