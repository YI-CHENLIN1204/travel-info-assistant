namespace TravelInfoAssistant.Api.Services;

public static class GeoDistance
{
    private const double EarthRadiusKilometers = 6371.0088;

    public static double CalculateKilometers(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        var latitudeDelta = DegreesToRadians(latitude2 - latitude1);
        var longitudeDelta = DegreesToRadians(longitude2 - longitude1);
        var originLatitude = DegreesToRadians(latitude1);
        var destinationLatitude = DegreesToRadians(latitude2);

        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(originLatitude)
            * Math.Cos(destinationLatitude)
            * Math.Pow(Math.Sin(longitudeDelta / 2), 2);

        return EarthRadiusKilometers * 2 * Math.Asin(Math.Sqrt(haversine));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
