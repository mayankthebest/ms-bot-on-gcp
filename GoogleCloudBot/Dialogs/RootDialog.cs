using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.Storage.V1;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;

namespace GoogleCloudBot.Dialogs
{
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);

            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var activity = await result;

            // return our reply to the user
            await context.PostAsync("Hi. I am a chat bot developed using Microsoft Bot Framework and running on Google Cloud Platform!");
            try
            {
                PromptDialog.Choice<string>(context, OnUserChoiceMade, new List<string> { "Upload an image to Google Cloud Storage", "View a random image from Google Cloud Storage" },
                        "Please select your choice");
            }
            catch (TooManyAttemptsException)
            {
                await context.PostAsync("Oops! You tried too many times.");
            }
        }

        private async Task OnUserChoiceMade(IDialogContext context, IAwaitable<string> result)
        {
            var choice = await result;
            if (choice.Contains("Upload"))
            {
                await context.PostAsync("Please select an image to upload.");
                context.Wait(GetImageAsync);
            }
            else if (choice.Contains("View"))
            {
                try
                {
                    var credential = GoogleCredential.FromFile(@"PATHTOKEY.json");
                    var client = StorageClient.Create(credential);
                    Bucket bucket = null;
                    bucket = await SetUpGCP(client, bucket);
                    bool noImagePresent = true;
                    IMessageActivity message = context.MakeMessage();

                    foreach (var obj in client.ListObjects(bucket.Name))
                    {
                        noImagePresent = false;
                        message.Attachments.Add(new Attachment()
                        {
                            ContentUrl = obj.MediaLink,
                            ContentType = obj.ContentType
                        });
                    }

                    await context.PostAsync(message);

                    if (noImagePresent)
                    {
                        message.Text = "Sorry. No images uploaded yet.";
                        await context.PostAsync(message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                context.EndConversation("See you soon");
            }
        }

        private static async Task<Bucket> SetUpGCP(StorageClient client, Bucket bucket)
        {
            try
            {
                bucket = await client.CreateBucketAsync("YOURPROJECTID", "BUCKETNAME", new CreateBucketOptions { PredefinedAcl = PredefinedBucketAcl.PublicReadWrite });
            }
            catch (Google.GoogleApiException e)
            when (e.Error.Code == 409)
            {
                // The bucket already exists.  That's fine.
                bucket = await client.GetBucketAsync("BUCKETNAME");
            }

            return bucket;
        }

        private async Task GetImageAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            try
            {
                var message = await result;
                if (message.Attachments.Count == 0)
                    await context.PostAsync("You should send an image file.");
                else
                {
                    var credential = GoogleCredential.FromFile(@"PATHTOKEY.json");
                    var client = StorageClient.Create(credential);
                    Bucket bucket = null;
                    bucket = await SetUpGCP(client, bucket);
                    // Upload some files
                    using (WebClient webClient = new WebClient())
                    {
                        using (Stream stream = webClient.OpenRead(message.Attachments[0].ContentUrl))
                        {
                            await client.UploadObjectAsync(bucket.Name, message.Attachments[0].Name, message.Attachments[0].ContentType, stream, new UploadObjectOptions { PredefinedAcl = PredefinedObjectAcl.PublicRead });
                        }
                    }

                    await context.PostAsync("Image Uploaded successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            context.EndConversation("See you soon");
        }
    }
}