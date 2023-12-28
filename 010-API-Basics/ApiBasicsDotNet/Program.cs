using System.Text;
using Azure.AI.OpenAI;
using dotenv.net;

// Get environment variables from .env file. We have to go up 6 levels to get to the root of the
// git repository (because of bin/Debug/net8.0 folder).
var env = DotEnv.Read(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 6));

var client = new OpenAIClient(env["OPENAI_API_KEY"]);

var chatCompletionOptions = new ChatCompletionsOptions(
  "gpt-4-1106-preview",
  [
    // System prompt
    new ChatRequestSystemMessage("""
      Du bist ein Assistent, der SchülerInnen hilft, festzustellen, ob unsere Schule
      die richtige für sie ist. Wenn ja hilfst du, auszuwählen, welche Abteilung für
      sie interessant ist. Zur Auwahl stehen:

      * Informatik - Schwerpunkt auf client- und serverseitige Softwareentwicklung,
                     Unterricht mehrerer Full-Stack Plattformen, Systemprogrammierung, Datenbanken, technisches Projektmanagement
      * Elektronik - Schwerpunkt auf Mechanik, Elektrotechnik, Halbleiterproduktion, Funktechnik, Systemprogrammierung
      * Medientechnik - Schwerpunkte Medienproduktion, Web Development, App-Entwicklung, Grundlagen Softwareentwicklung

      In allen Abteilungen MUSS man Interesse an technischen Fragestellungen haben und DARF KEINE Abneigung gegenüber
      Mathematik, Physik, Logik & Co haben. In allen Abteilungen MUSS man auf jeden Fall gerne programmieren lernen wollen.
      Falls das nicht zutrifft, erkläre, dass unsere Schule möglicherweise die falsche Ausbildungsrichtung ist.

      Die SchülerInnen sind zwischen 12 und 14 Jahren alt und haben noch keine technische Erfahrung in den genannten Bereichen.
      Vermeide daher technische Fachbegriffe. Halte die Antworten kurz und prägant. Die Antwort darf KEINE Zeilenumbrüche enthalten.
      Stelle Fragen bis du dein Eindruck hast, eine Empfehlung abgeben zu können.
      """),
    // Initial assistant message to get the conversation started
    new ChatRequestAssistantMessage("""
      Hallo! Ich kann dir helfen, herauszufinden, ob unsere Schule die richtige für dich ist. Erzähle mir etwas über dich?
      Warum interessierst du dich für unsere Schule?
      """),
  ]
);

Console.OutputEncoding = Encoding.UTF8; // This should help displaying emojis in the console

while (true)
{
  // Display the last message from the assistant
  if (chatCompletionOptions.Messages.Last() is ChatRequestAssistantMessage am)
  {
    Console.WriteLine($"🤖: {am.Content}");
  }

  // Ask the user for a message. Exit program in case of empty message.
  Console.Write("\nYou (just press enter to exit the conversation): ");
  var userMessage = Console.ReadLine();
  if (string.IsNullOrEmpty(userMessage)) { break; }

  // Add the user message to the list of messages to send to the API
  chatCompletionOptions.Messages.Add(new ChatRequestUserMessage(userMessage));

  // Send the messages to the API and wait for the response. Display a
  // waiting indicator while waiting for the response.
  Console.Write("\nThinking...");
  var chatTask = client.GetChatCompletionsAsync(chatCompletionOptions);
  while (!chatTask.IsCompleted)
  {
    Console.Write(".");
    await Task.Delay(1000);
  }

  Console.WriteLine("\n");
  var response = await chatTask;
  if (response.GetRawResponse().IsError)
  {
    Console.WriteLine($"Error: {response.GetRawResponse().ReasonPhrase}");
    break;
  }

  // Add the response from the API to the list of messages to send to the API
  chatCompletionOptions.Messages.Add(new ChatRequestAssistantMessage(response.Value.Choices[0].Message));
}
