using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace App1
{
    internal class OpenAIAssistant
    {
        /** INSERT YOUR OWN NAME / ENDPOINT / KEY HERE **/
        private const string deploymentName = "";
        private const string endpoint = "";
        private const string key = "";

        private AzureOpenAIClient _openAIClient;

        public OpenAIAssistant()
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(endpoint))
            {
                throw new InvalidOperationException("You must provide an endpoint and a key in order to initialize the AI client.");
            }

            _openAIClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));
        }

        public async Task<ChatCompletion> QueryImageAsync(byte[] imageData, List<string> queries, string systemPrompt)
        {
            // Limit image to 2MB for quick response and less tokens used
            if (imageData.Length > 2097152)
            {
                throw new ArgumentException("Image exceeded 2MB, try downsizing the image");
            }

            if (imageData.Length == 0)
            {
                throw new ArgumentException("Image is invalid");
            }

            var chatClient = _openAIClient.GetChatClient(deploymentName);
            var chatContent = ChatMessageContentPart.CreateImagePart(imageBytes: BinaryData.FromBytes(imageData), "image/png");
            var systemPrompt = new SystemChatMessage(systemPrompt);
            var imagePrompt = new UserChatMessage(chatContent);

            var chatMessages = new List<ChatMessage>
            {
                systemPrompt,
                imagePrompt,
            };

            foreach (var query in queries)
            {
                var userChatMessage = new UserChatMessage(query);
                chatMessages.Add(userChatMessage);
            }

            var chatCompletionOptions = new ChatCompletionOptions() { ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat() };
            ChatCompletion chatCompletion = await chatClient.CompleteChatAsync(chatMessages, chatCompletionOptions);
            return chatCompletion;
        }
    }
}
