using System.Text.Json.Serialization;

record struct Unterrichtsstunden(
  int Stunden,
  int Uebung
);

record Stundentafel(
  string Schultyp,
  [property: JsonPropertyName("Stundentafel")] GegenstandStunden[] GegenstandStunden
);

record GegenstandStunden(
  string Gegenstand,
  [property: JsonPropertyName("JG 1")] Unterrichtsstunden JG1,
  [property: JsonPropertyName("JG 2")] Unterrichtsstunden JG2,
  [property: JsonPropertyName("JG 3")] Unterrichtsstunden JG3,
  [property: JsonPropertyName("JG 4")] Unterrichtsstunden JG4,
  [property: JsonPropertyName("JG 5")] Unterrichtsstunden JG5
);

record StundentafelAbfrage(
  [property: JsonPropertyName("fach")] string Fach,
  [property: JsonPropertyName("jahrgang")] int Jahrgang
);
