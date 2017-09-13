using System;
using System.Collections.Generic;

namespace BonVoyage
{
	public class PathUtils
	{
		internal struct WayPoint {
			public double latitude;
			public double longitude;
			public WayPoint(double lat, double lon) {
				latitude = lat;
				longitude = lon;
			}
		}

		/// <summary>
		/// Encodes the path.
		/// </summary>
		/// <returns>The path.</returns>
		/// <param name="path">Path.</param>
		internal static string EncodePath(Path<Hex> path) {
			string result = "";
			foreach (Hex point in path) {
				result += point.Latitude.ToString("R") + ":" + point.Longitude.ToString("R") +";";
			}
            //return LZString.compressToBase64 (result);

            // Change LZString implementation of base64 to native functions
            // Replace forward slash with # (two forward slashes seems to be interpreted as a start of the comment when read from a save file)
            var textBytes = System.Text.Encoding.UTF8.GetBytes(result);
            return System.Convert.ToBase64String(textBytes).Replace('/', '#');
        }

		/// <summary>
		/// Decodes the path.
		/// </summary>
		/// <returns>The path.</returns>
		/// <param name="pathEncoded">Path encoded.</param>
		/// <param name="body">Body.</param>
		internal static List<Vector3d> DecodePath(string pathEncoded, CelestialBody body) {
			List<Vector3d> result = new List<Vector3d> ();

			if (pathEncoded == null || pathEncoded.Length == 0)
				return result;

            // Path is compressed, decompress
            // For compatibility purposes only
            if (!pathEncoded.Contains(";"))
            {
                //pathEncoded = LZString.decompressFromBase64(pathEncoded);

                // Change LZString implementation of base64 to native functions
                // Replace # with forward slash (two forward slashes seems to be interpreted as a start of the comment when read from a save file)
                string temp = pathEncoded;
                var encodedBytes = System.Convert.FromBase64String(temp.Replace('#', '/'));
                temp = System.Text.Encoding.UTF8.GetString(encodedBytes);
                if (temp.Contains(":")) // backward compatibility for path encoded with LZString
                    pathEncoded = temp;
                else
                    pathEncoded = LZString.decompressFromBase64(pathEncoded);
            }

			char[] separators = new char[] { ';' };
			string[] wps = pathEncoded.Split (separators, StringSplitOptions.RemoveEmptyEntries);

			foreach (var wp in wps) {
				string[] latlon = wp.Split (':');
				double latitude = double.Parse (latlon [0]);
				double longitude = double.Parse (latlon [1]);
				double altitude = GeoUtils.TerrainHeightAt (latitude, longitude, body);
				Vector3d localSpacePoint = body.GetWorldSurfacePosition (latitude, longitude, altitude);
				result.Add (localSpacePoint);
			}

			return result;
		}

		/// <summary>
		/// Decodes the path.
		/// </summary>
		/// <returns>The path.</returns>
		/// <param name="pathEncoded">Path encoded.</param>
		internal static List<WayPoint> DecodePath(string pathEncoded) {
			if (pathEncoded == null || pathEncoded.Length == 0)
				return null;

            // Path is compressed, decompress
            // For compatibility purposes only
            if (!pathEncoded.Contains(";"))
            {
                //pathEncoded = LZString.decompressFromBase64(pathEncoded);

                // Change LZString implementation of base64 to native functions
                // Replace # with forward slash (two forward slashes seems to be interpreted as a start of the comment when read from a save file)
                string temp = pathEncoded;
                var encodedBytes = System.Convert.FromBase64String(temp.Replace('#', '/'));
                temp = System.Text.Encoding.UTF8.GetString(encodedBytes);
                if (temp.Contains(":")) // backward compatibility for path encoded with LZString
                    pathEncoded = temp;
                else
                    pathEncoded = LZString.decompressFromBase64(pathEncoded);
            }

			List<WayPoint> result = new List<WayPoint> ();
			char[] separators = new char[] { ';' };
			string[] wps = pathEncoded.Split (separators, StringSplitOptions.RemoveEmptyEntries);
			foreach (var wp in wps) {
				string[] latlon = wp.Split (':');
				result.Add (new WayPoint (double.Parse(latlon [0]), double.Parse(latlon [1])));
			}
			result.Reverse ();
			return result;
		}
	}
}

