using System.Text.Json;
using TravelInfoAssistant.Api.Providers.MetNorway;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class MetNorwayWeatherMapperTests
{
    [Fact]
    public void Map_MapsCurrentHourlyAndDailyForecasts()
    {
        const string json = """
            {
              "type": "Feature",
              "geometry": {
                "type": "Point",
                "coordinates": [121.5637, 25.0375, 10]
              },
              "properties": {
                "meta": {
                  "updated_at": "2026-09-24T00:30:00Z"
                },
                "timeseries": [
                  {
                    "time": "2026-09-24T01:00:00Z",
                    "data": {
                      "instant": {
                        "details": {
                          "air_temperature": 28.0,
                          "relative_humidity": 75.0,
                          "wind_speed": 2.0,
                          "wind_from_direction": 90.0
                        }
                      },
                      "next_1_hours": {
                        "summary": { "symbol_code": "partlycloudy_day" },
                        "details": {
                          "precipitation_amount": 0.2,
                          "probability_of_precipitation": 35.0
                        }
                      }
                    }
                  },
                  {
                    "time": "2026-09-24T04:00:00Z",
                    "data": {
                      "instant": {
                        "details": {
                          "air_temperature": 31.0,
                          "relative_humidity": 65.0,
                          "wind_speed": 3.0,
                          "wind_from_direction": 120.0
                        }
                      },
                      "next_1_hours": {
                        "summary": { "symbol_code": "lightrainshowers_day" },
                        "details": {
                          "precipitation_amount": 1.1,
                          "probability_of_precipitation": 70.0
                        }
                      }
                    }
                  }
                ]
              }
            }
            """;

        var forecast = JsonSerializer.Deserialize<MetNorwayForecast>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(forecast);
        var result = MetNorwayWeatherMapper.Map(
            forecast,
            "台北",
            "Asia/Taipei",
            0,
            0);

        Assert.Equal("台北", result.LocationName);
        Assert.Equal(25.0375, result.Latitude);
        Assert.Equal(121.5637, result.Longitude);
        Assert.Equal(28, result.Current.TemperatureCelsius);
        Assert.Equal(31.9, result.Current.ApparentTemperatureCelsius);
        Assert.Equal("partlycloudy", result.Current.ConditionCode);
        Assert.Equal("局部多雲", result.Current.ConditionLabel);
        Assert.Equal(2, result.Hourly.Count);
        Assert.Single(result.Daily);
        Assert.Equal(28, result.Daily[0].MinimumTemperatureCelsius);
        Assert.Equal(31, result.Daily[0].MaximumTemperatureCelsius);
        Assert.Equal(1.3, result.Daily[0].PrecipitationMillimeters);
        Assert.Equal(70, result.Daily[0].PrecipitationProbabilityPercent);
    }

    [Theory]
    [InlineData("clearsky_night", "晴朗")]
    [InlineData("heavyrainandthunder", "雷雨")]
    [InlineData("lightsnowshowers_day", "降雪")]
    [InlineData("fog", "有霧")]
    public void GetConditionLabel_MapsProviderCodes(string code, string expected)
    {
        Assert.Equal(expected, MetNorwayWeatherMapper.GetConditionLabel(code));
    }

    [Fact]
    public void CalculateApparentTemperature_FallsBackToAirTemperatureWhenInputsMissing()
    {
        Assert.Equal(22.4, MetNorwayWeatherMapper.CalculateApparentTemperature(22.4, null, null));
    }
}
