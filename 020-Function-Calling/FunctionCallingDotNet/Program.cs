using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using dotenv.net;

var env = DotEnv.Read(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 6));

var client = new OpenAIClient(env["OPENAI_API_KEY"]);

var chatCompletionOptions = new ChatCompletionsOptions(
  "gpt-4-1106-preview",
  [
    // System prompt
    new ChatRequestSystemMessage("""
      Du bist ein Assistent, der SchülerInnen hilft, festzustellen, ob unsere Schule die richtige für
      sie ist. Du kannst Fragen über die unterrichteten Gegenstände, insbesondere über die Anzahl
      an Wochenstunden pro Jahrgang (1 bis 5) beantworten.
      """),
    // Initial assistant message to get the conversation started
    new ChatRequestAssistantMessage("""
      Hallo! Du kannst mir Fragen über die unterrichteten Gegenstände, insbesondere über die Anzahl
      an Wochenstunden pro Jahrgang (1 bis 5) stellen.
      """),
  ]
)
{
  // Define the tool functions that can be called from the assistant
  Functions =
  {
    new FunctionDefinition()
    {
      Name = "getUnterrichtsfaecher",
      Description = """
        Liefert eine Liste der Fächer, die an der Schule unterrichtet werden. Diese Funktion MUSS verwendet
        werden, um eingegebene Namen von Fächer zu validieren.
        """
    },
    new FunctionDefinition()
    {
      Name = "getUnterrichtsstundenProWoche",
      Description = """
        Liefert die Anzahl an Unterrichtsstunden pro Woche für ein bestimmtes Fach und einen
        bestimmten Jahrgang. Wenn das Ergebnis 0 ist, wird das Fach in diesem Jahrgang nicht unterrichtet.
        """,
      Parameters = BinaryData.FromObjectAsJson(
        new
        {
          Type = "object",
          Properties = new
          {
            Fach = new
            {
              Type = "string",
              Description = "Der Name des Faches. Erlaubt sind alle Fächer, die von der Funktion getUnterrichtsfaecher geliefert werden."
            },
            Jahrgang = new
            {
              Type = "integer",
              Description = "Der Jahrgang, für den die Anzahl an Unterrichtsstunden pro Woche ermittelt werden soll. Erlaubte Werte sind 1 bis 5."
            }
          },
          Required = new[] { "fach", "jahrgang" }
        },
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
    }
  }
};

Console.OutputEncoding = Encoding.UTF8;

// Read the stundentafel.json file and deserialize it into a Stundentafel object.
// This object will be used to answer tool calls from ChatGPT.
var stundentafel = JsonSerializer.Deserialize<Stundentafel>(await File.ReadAllTextAsync("../stundentafel.json"));
Debug.Assert(stundentafel != null);
var gegenstaende = stundentafel.GegenstandStunden.Select(gs => gs.Gegenstand).Distinct();

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

  bool repeat;
  do
  {
    // If the last message from the assistant was a tool call, we need to
    // add the tool call result to the list of messages to send to the API.
    // We also need to repeat the call to the API to get the next message
    // from the assistant. The next message could be another tool call.
    // We have to repeat that process until the assistant sends a message
    // that is not a tool call.
    repeat = false;

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

    if (response.Value.Choices[0].Message.ToolCalls.Any())
    {
      // We have a tool call

      // NOTE that the API recently added the ability to call multiple tools in one request.
      // That allows you to process tool calls in parallel (if that speeds up your application).
      // This sample code does not parallelize tool calls because it would make the code more
      // complex and therefore it would be harder to understand the OpenAI API.

      foreach (var toolCall in response.Value.Choices[0].Message.ToolCalls.OfType<ChatCompletionsFunctionToolCall>())
      {
        Console.WriteLine($"\tExecuting tool {toolCall.Name} with arguments {toolCall.Arguments}.");
        ChatRequestToolMessage result;
        switch (toolCall.Name)
        {
          case "getUnterrichtsfaecher":
            {
              result = new ChatRequestToolMessage(JsonSerializer.Serialize(gegenstaende), toolCall.Id);
              break;
            }
          case "getUnterrichtsstundenProWoche":
            {
              // Deserialize arguments
              var parameter = JsonSerializer.Deserialize<StundentafelAbfrage>(toolCall.Arguments);
              Debug.Assert(parameter != null);
              var gegenstand = stundentafel.GegenstandStunden.FirstOrDefault(gs => gs.Gegenstand == parameter.Fach);
              if (gegenstand == null)
              {
                var responseObject = new { Error = "Unbekanntes Fach. Es dürfen nur Fächer übergeben werden, die von der Funktion getUnterrichtsfaecher zurückgegeben wurden" };
                result = new ChatRequestToolMessage(JsonSerializer.Serialize(responseObject), toolCall.Id);
                break;
              }

              // Get result from Stundentafel
              var stunden = parameter.Jahrgang switch
              {
                1 => gegenstand.JG1,
                2 => gegenstand.JG2,
                3 => gegenstand.JG3,
                4 => gegenstand.JG4,
                5 => gegenstand.JG5,
                _ => throw new InvalidOperationException($"Jahrgang {parameter.Jahrgang} ist nicht erlaubt.")
              };
              result = new ChatRequestToolMessage(JsonSerializer.Serialize(stunden), toolCall.Id);
              break;
            }
          default:
            throw new InvalidOperationException($"Tool {toolCall.Name} is not supported.");
        }

        // Add the result of the tool call to the list of messages to send to the API
        chatCompletionOptions.Messages.Add(result);
        repeat = true;
      }
    }
    else
    {
      // We don't have a tool call. Add the response from the API to the list of messages to send to the API
      chatCompletionOptions.Messages.Add(new ChatRequestAssistantMessage(response.Value.Choices[0].Message));
    }
  } while (repeat);
}
