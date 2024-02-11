using System.Text;
using Azure.AI.OpenAI;
using dotenv.net;

var env = DotEnv.Read(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 6));

var client = new OpenAIClient(env["OPENAI_API_KEY"]);

ChatRequestAssistantMessage starterMessage;
var chatCompletionOptions = new ChatCompletionsOptions(
  "gpt-4-1106-preview",
  [
    // System prompt
    new ChatRequestSystemMessage("""
      Du bist ein Assistent, der SchülerInnen hilft, festzustellen, ob unsere Schule die richtige für
      sie ist. Unsere Schule ist spezialisiert auf Informatik. Dementsprechend wichtig sind technische
      Fächer wie Mathematik, Physik und Informatik. Zusätzlich liegt noch ein Schwerpunkt auf wirtschaftliche
      Themen wie Betriebswirtschaft und Rechnungswesen.
      """),
    // Initial assistant message to get the conversation started
    starterMessage = new ChatRequestAssistantMessage("""
      Hallo! Bist du unsicher, ob unsere Schule das richtige für dich ist? Stelle mir Fragen
      oder erzähle mir, was dich beschäftigt. Vielleicht kann ich dir helfen.
      """),
  ]
);

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine($"🤖: {starterMessage.Content}");

while (true)
{
  // Ask the user for a message. Exit program in case of empty message.
  // A possible input would be "In manchen Fächern bin ich gut, in anderen weniger. Welche Fächer sind wichtig für diese Schule?"
  Console.Write("\nYou (just press enter to exit the conversation): ");
  var userMessage = Console.ReadLine();
  if (string.IsNullOrEmpty(userMessage)) { break; }

  // Add the user message to the list of messages to send to the API
  chatCompletionOptions.Messages.Add(new ChatRequestUserMessage(userMessage));

  // Get the response from the API using the streaming API. Uses SSE behind the scenes.
  Console.Write("\n🤖: ");
  var response = await client.GetChatCompletionsStreamingAsync(chatCompletionOptions);
  if (response.GetRawResponse().IsError)
  {
    Console.WriteLine($"Error: {response.GetRawResponse().ReasonPhrase}");
    break;
  }

  // Build the response from the API. The response is a stream of messages. We need to enumerate over the stream.
  var messageBuilder = new StringBuilder();
  await foreach (var choices in response)
  {
    Console.Write(choices.ContentUpdate);
    messageBuilder.Append(choices.ContentUpdate);
  }

  Console.WriteLine("\n");

  // Add the response from the API to the list of messages to send to the API
  chatCompletionOptions.Messages.Add(new ChatRequestAssistantMessage(messageBuilder.ToString()));
}
