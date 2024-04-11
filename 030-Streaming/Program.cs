using System.Text;
using Azure.AI.OpenAI;
using dotenv.net;

var env = DotEnv.Read(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 6));

var client = new OpenAIClient(env["OPENAI_API_KEY"]);

ChatRequestAssistantMessage starterMessage;
var chatCompletionOptions = new ChatCompletionsOptions(
  "gpt-4-1106-preview",
  [
    // System prompt (see also prompt-de.txt and prompt-en.txt)
    new ChatRequestSystemMessage("""
      You are an assistant who helps students determine if our school is the right one for them.
      Our school specializes in computer science. Accordingly, technical subjects such as mathematics,
      physics, and computer science are very important. Additionally, there is a strong emphasis
      on economic subjects like business administration and accounting.
      """),
    // Initial assistant message to get the conversation started
    starterMessage = new ChatRequestAssistantMessage("""
      Hello! Are you unsure if our school is the right one for you? Ask me questions or tell me
      what's on your mind. Maybe I can help.
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
