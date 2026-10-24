namespace TravelInfoAssistant.Api.Services.Flights;

internal static class AirportChineseNames
{
    // City aliases intentionally match multiple airports where a city has more than one.
    private static readonly IReadOnlyDictionary<string, (string Name, string City, string Aliases)> Names =
        new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["TPE"] = ("桃園國際機場", "桃園", "台北 臺北 桃園 台灣 臺灣"),
            ["TSA"] = ("台北松山機場", "台北", "臺北 松山 台灣 臺灣"),
            ["RMQ"] = ("台中國際機場", "台中", "臺中 清泉崗 台灣 臺灣"),
            ["KHH"] = ("高雄國際機場", "高雄", "小港 台灣 臺灣"),
            ["TNN"] = ("台南機場", "台南", "臺南 台灣 臺灣"),
            ["HUN"] = ("花蓮機場", "花蓮", "台灣 臺灣"),
            ["TTT"] = ("台東機場", "台東", "臺東 豐年 台灣 臺灣"),
            ["MZG"] = ("澎湖機場", "澎湖", "馬公 台灣 臺灣"),
            ["KNH"] = ("金門機場", "金門", "尚義 台灣 臺灣"),
            ["MFK"] = ("馬祖北竿機場", "馬祖北竿", "連江 馬祖 北竿 台灣 臺灣"),
            ["LZN"] = ("馬祖南竿機場", "馬祖南竿", "連江 馬祖 南竿 台灣 臺灣"),
            ["KYD"] = ("蘭嶼機場", "蘭嶼", "台東 臺東 台灣 臺灣"),
            ["CYI"] = ("嘉義機場", "嘉義", "水上 台灣 臺灣"),
            ["HKG"] = ("香港國際機場", "香港", "赤鱲角 赤臘角"),
            ["MFM"] = ("澳門國際機場", "澳門", "澳门"),
            ["NRT"] = ("東京成田國際機場", "東京", "成田 日本"),
            ["HND"] = ("東京羽田機場", "東京", "羽田 日本"),
            ["KIX"] = ("大阪關西國際機場", "大阪", "關西 京都 日本"),
            ["ITM"] = ("大阪伊丹機場", "大阪", "伊丹 日本"),
            ["FUK"] = ("福岡機場", "福岡", "九州 日本"),
            ["OKA"] = ("沖繩那霸機場", "沖繩", "那霸 日本"),
            ["CTS"] = ("札幌新千歲機場", "札幌", "北海道 新千歲 日本"),
            ["NGO"] = ("名古屋中部國際機場", "名古屋", "中部 日本"),
            ["ICN"] = ("首爾仁川國際機場", "首爾", "仁川 韓國"),
            ["GMP"] = ("首爾金浦國際機場", "首爾", "金浦 韓國"),
            ["PUS"] = ("釜山金海國際機場", "釜山", "金海 韓國"),
            ["SIN"] = ("新加坡樟宜機場", "新加坡", "樟宜"),
            ["BKK"] = ("曼谷蘇凡納布機場", "曼谷", "素萬那普 泰國"),
            ["DMK"] = ("曼谷廊曼機場", "曼谷", "廊曼 泰國"),
            ["KUL"] = ("吉隆坡國際機場", "吉隆坡", "馬來西亞"),
            ["SGN"] = ("胡志明市新山一機場", "胡志明市", "西貢 越南"),
            ["HAN"] = ("河內內排國際機場", "河內", "越南"),
            ["DAD"] = ("峴港國際機場", "峴港", "越南"),
            ["LHR"] = ("倫敦希斯洛機場", "倫敦", "希斯洛 英國"),
            ["CDG"] = ("巴黎戴高樂機場", "巴黎", "戴高樂 法國"),
            ["LAX"] = ("洛杉磯國際機場", "洛杉磯", "美國"),
            ["SFO"] = ("舊金山國際機場", "舊金山", "三藩市 美國"),
            ["JFK"] = ("紐約甘迺迪國際機場", "紐約", "甘迺迪 美國")
        };

    public static string SearchText(string iata) => Names.TryGetValue(iata, out var value)
        ? $"{value.Name} {value.City} {value.Aliases}"
        : string.Empty;

    public static (string Name, string City)? Find(string iata) =>
        Names.TryGetValue(iata, out var value) ? (value.Name, value.City) : null;
}
