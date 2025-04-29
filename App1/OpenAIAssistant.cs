using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace App1
{
    internal class OpenAIAssistant
    {
        private const string deploymentName = "2025-hackathon-ai-gpt-4o-mini";
        private const string endpoint = "https://2025-hackathon-ai.openai.azure.com";
        private const string key = "";

        private AzureOpenAIClient _openAIClient;

        public OpenAIAssistant()
        {
            _openAIClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));
        }

        public async Task<ChatCompletion> QueryImageAsync(byte[] imageData, List<string> queries)
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
            var systemPrompt = new SystemChatMessage("You are a helpful assistant knowledgeable about trees");
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

            var chatCompletionOptions = new ChatCompletionOptions() { ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()};
            ChatCompletion chatCompletion = await chatClient.CompleteChatAsync(chatMessages, chatCompletionOptions);
            return chatCompletion;
        }
    }
}
