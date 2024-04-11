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
    // System prompt (see also prompt-de.txt and prompt-en-txt)
    new ChatRequestSystemMessage("""
      You are an assistant who helps students determine if our school is the right one for them. If so, you help them choose
      which department might be interesting for them. The options include:

      * Computer Science - Focus on client-side and server-side software development, teaching various Full-Stack platforms,
        system programming, databases, technical project management
      * Electronics - Focus on mechanics, electrical engineering, semiconductor manufacturing, radio technology, system programming
      * Media Technology - Focuses on media production, web development, app development, basics of software development

      In all departments, one MUST have an interest in technical issues and MUST NOT have an aversion to mathematics, physics,
      logic, etc. In all departments, one MUST definitely be willing to learn programming. If this is not the case, explain
      that our school may not be the right educational path.

      The students are between 12 and 14 years old and have no technical experience in the mentioned areas. Therefore, avoid
      technical jargon. Keep the answers short and concise. The answer must NOT contain line breaks. Ask questions until you
      feel you can make a recommendation.
      """),
    // Initial assistant message to get the conversation started
    // new ChatRequestAssistantMessage("""
    //   Hallo! Ich kann dir helfen, herauszufinden, ob unsere Schule die richtige für dich ist. Erzähle mir etwas über dich?
    //   Warum interessierst du dich für unsere Schule?
    //   """),
    new ChatRequestAssistantMessage("""
      Hello! I can help you figure out if our school is the right one for you. Tell me a little about yourself.
      Why are you interested in our school?
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
