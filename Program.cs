using System.Text;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;

namespace ChatGptCaller;

/// <summary>
/// A simple app to run ChatGPT queries from the terminal.
/// </summary>
public class Program
{
    private readonly OpenAIClient client;
    const string ApiKeyFile = "open-api-key.txt";

    public static async Task Main(string[] args)
    {
        // Step 1: Get the API key.
        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            var fn = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ApiKeyFile);
            if (File.Exists(fn))
                key = await File.ReadAllTextAsync(fn);
        }

        if (string.IsNullOrEmpty(key))
        {
            Console.WriteLine(
                "Missing OpenAPI key. Set the OPENAI_API_KEY environment variable or create a file named open-api-key.txt in your MyDocuments folder.");
            return;
        }

        var api = new OpenAIClient(key);
        var program = new Program(api);
        
        await program.Run(args);
    }

    private Program(OpenAIClient client)
    {
        this.client = client;
    }

    private async Task Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Missing query.");
            return;
        }

        if (args.Length == 1 && string.Compare("list", args[0], StringComparison.CurrentCultureIgnoreCase) == 0)
        {
            var models = await client.ModelsEndpoint.GetModelsAsync();
            models.ToList().ForEach(m => Console.WriteLine(m));
            return;
        }

        string model = Model.GPT3_5_Turbo;
        bool interactive = false;
        var sb = new StringBuilder();

        foreach (var arg in args)
        {
            if (arg.StartsWith("--model="))
            {
                model = arg[8..];
            }
            else if (arg.StartsWith("-m="))
            {
                model = arg[3..];
            }
            else if (string.Compare("--interactive", arg, StringComparison.CurrentCultureIgnoreCase) == 0 
                     || string.Compare("-i", arg, StringComparison.CurrentCultureIgnoreCase) == 0)
            {
                interactive = true;
            }
            else
            {
                sb.Append(arg);
                sb.Append(' ');
            }
        }

        if (string.IsNullOrEmpty(model))
            model = Model.GPT3_5_Turbo;

        var chatPrompts = new List<ChatPrompt>
        {
            new("system", "You are a helpful assistant."),
        };

        var question = sb.ToString();

        while (true)
        {
            chatPrompts.Add(new("user", question));
            var chatRequest = new ChatRequest(chatPrompts, model);

            sb = new StringBuilder();
            await foreach (var result in client.ChatEndpoint.StreamCompletionEnumerableAsync(chatRequest))
            {
                var choice = result.FirstChoice;
                if (choice.Delta.Content != null)
                {
                    sb.Append(choice.Delta.Content);
                    Console.Write(choice);
                }
            }

            chatPrompts.Add(new("assistant", sb.ToString()));
            sb.Clear();

            Console.WriteLine();
            if (!interactive) break;

            Console.Write("> ");
            question = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(question) || string.Compare("quit", question, StringComparison.CurrentCultureIgnoreCase) == 0)
                break;
        }
    }

}