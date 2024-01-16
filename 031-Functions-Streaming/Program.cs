using System.Diagnostics;
using System.Text;
using System.Text.Json;
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
)
{
  // Define the tool functions that can be called from the assistant
  Tools =
    {
        new ChatCompletionsFunctionToolDefinition(
            new FunctionDefinition()
            {
                Name = "getPrioritaetJeFach",
                Description = """
                    Liefert die Priorität (1-5, 1 bedeutet sehr wichtig, 5 bedeutet unwichtig) eines Faches an unserer Schule.
                    Wenn 0 zurückgegeben wird, ist das Fach an unserer Schule nicht vorhanden.
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
                            Description = "Der Name des Faches."
                        },
                    },
                    Required = new[] { "fach" }
                },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            })
    }
};

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine($"🤖: {starterMessage.Content}");

while (true)
{
  // Ask the user for a message. Exit program in case of empty message.
  // A possible input would be "In manchen Fächern bin ich gut, in anderen weniger. Welche Fächer sind wichtig für diese Schule?"
  Console.Write("\nYou (just press enter to exit the conversation): ");
  var userMessage = "Was ist in der Schule wichtiger, Informatik oder Rechnungswesen?"; //Console.ReadLine();
  if (string.IsNullOrEmpty(userMessage)) { break; }

  // Add the user message to the list of messages to send to the API
  chatCompletionOptions.Messages.Add(new ChatRequestUserMessage(userMessage));

  bool repeat;
  do
  {
    repeat = false;

    var response = await client.GetChatCompletionsStreamingAsync(chatCompletionOptions);
    if (response.GetRawResponse().IsError)
    {
      Console.WriteLine($"Error: {response.GetRawResponse().ReasonPhrase}");
      break;
    }

    var messageBuilder = new StringBuilder();
    var functionCalls = new List<FunctionCall>();
    var currentFunctionCall = new FunctionCall();
    var currentToolIndex = -1;
    var firstOutput = true;
    await foreach (var choices in response.EnumerateValues())
    {
      if (choices.ToolCallUpdate != null)
      {
        var update = choices.ToolCallUpdate as StreamingFunctionToolCallUpdate;
        Debug.Assert(update != null);
        if (update.ToolCallIndex != currentToolIndex && currentToolIndex != -1)
        {
          functionCalls.Add(currentFunctionCall);
          currentFunctionCall = new();
        }

        currentToolIndex = update.ToolCallIndex;

        if (currentFunctionCall.Id == null && !string.IsNullOrEmpty(update.Id)) { currentFunctionCall.Id = update.Id; }
        if (currentFunctionCall.Name == null && !string.IsNullOrEmpty(update.Name)) { currentFunctionCall.Name = update.Name; }
        currentFunctionCall.ArgumentJson.Append(update.ArgumentsUpdate);
      }
      else
      {
        if (firstOutput)
        {
          Console.Write("\n🤖: ");
          firstOutput = false;
        }

        Console.Write(choices.ContentUpdate);
        messageBuilder.Append(choices.ContentUpdate);
      }

    }

    if (!functionCalls.Contains(currentFunctionCall)) { functionCalls.Add(currentFunctionCall); }

    if (messageBuilder.Length > 0)
    {
      chatCompletionOptions.Messages.Add(new ChatRequestAssistantMessage(messageBuilder.ToString()));
    }
    else if (functionCalls.Count > 0)
    {
      foreach (var f in functionCalls)
      {
        // TODO: Finish this implementation
        // This is currently blocked because of https://github.com/Azure/azure-sdk-for-net/issues/41274
      }

      foreach (var f in functionCalls)
      {
        if (f.Name != "getPrioritaetJeFach") { throw new NotImplementedException($"Function {f.Name} is not implemented."); }
        var parameter = JsonSerializer.Deserialize<FunctionCallArgument>(f.ArgumentJson.ToString());
        Debug.Assert(parameter != null);
        var priority = parameter.Fach switch
        {
          "Informatik" => 1,
          "Mathematik" => 1,
          "Physik" => 2,
          "Betriebswirtschaft" => 2,
          "Rechnungswesen" => 2,
          "Geographie" => 3,
          "Geschichte" => 3,
          _ => 0
        };

        var result = new ChatRequestToolMessage(JsonSerializer.Serialize(new { Prioritaet = priority }), f.Id);
        chatCompletionOptions.Messages.Add(result);
      }

      repeat = true;
    }
  }
  while (repeat);

  Console.WriteLine("\n");
}

class FunctionCall
{
  public string? Id { get; set; }
  public string? Name { get; set; }
  public StringBuilder ArgumentJson { get; private set; } = new();
}

record FunctionCallArgument(string Fach);
