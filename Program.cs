using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace app
{
    class Program
    {
        public static int[][] MatrixMultiplication(int[][] matrixA, int[][] matrixB)
        {

            int rowsA = matrixA.Length;
            int colsA = (rowsA > 0) ? matrixA[0].Length : 0;
            int rowsB = matrixB.Length;
            int colsB = (rowsB > 0) ? matrixB[0].Length : 0;

            if (colsA != rowsB)
            {
                throw new ArgumentException("Invalid matrix dimensions for multiplication");
            }

            int[][] result = new int[rowsA][];
            for (int i = 0; i < rowsA; i++)
            {
                result[i] = new int[colsB];
                for (int j = 0; j < colsB; j++)
                {
                    int sum = 0;
                    for (int k = 0; k < colsA; k++)
                    {
                        sum += matrixA[i][k] * matrixB[k][j];
                    }
                    result[i][j] = sum;
                }
            }

            return result;
        }

        static string ComputeMD5(string s)
        {
            StringBuilder sb = new StringBuilder();
    
            using (MD5 md5 = MD5.Create())
            {
                byte[] hashValue = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
                foreach (byte b in hashValue) {
                    sb.Append($"{b:X2}");
                }
            }
            return sb.ToString();
        }

        static async Task Main(string[] args)
        {
            int size = 1000;
            // Init the datasets
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync($"https://recruitment-test.investcloud.com/api/numbers/init/{size}");
                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        Console.WriteLine(responseBody);
                    }
                    else
                    {
                        Console.WriteLine("Request failed with status code: " + response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
            }

            Stopwatch sw = Stopwatch.StartNew();

            DatasetClient datasetClient = new DatasetClient();
            int[][] datasetA = await datasetClient.GetWholeDatasetBatches("A", "row", size);
            int[][] datasetB = await datasetClient.GetWholeDatasetBatches("B", "row", size);
            int[][] result = MatrixMultiplication(datasetA, datasetB);
            
            TimeSpan elapsedTime = sw.Elapsed;
            sw.Stop();
            StringBuilder sb = new();
            for (int i = 0; i < result.Length; i++)
            {
                for (int j = 0; j < result[i].Length; j++)
                {
                    sb.Append(result[i][j]);
                }
            }
            string resultString = sb.ToString();
            string hashValue = ComputeMD5(resultString);

            Console.WriteLine(elapsedTime);
            Console.WriteLine(hashValue);

            using (HttpClient client = new())
            {
                string requestBody = $"<string xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/\">{hashValue}</string>";
                var requestContent = new StringContent(requestBody);
                requestContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
                HttpResponseMessage response = await client.PostAsync("https://recruitment-test.investcloud.com/api/numbers/validate", requestContent);
                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Response: " + responseContent);
                }
                else
                {
                    Console.WriteLine("Request failed with status code: " + response.StatusCode);
                }
            }

        }

    }

    public class DatasetClient
    {
        private HttpClient client;

        public DatasetClient()
        {
            client = new HttpClient();
        }

        public async Task<int[]> GetDatasetValue(string dataset, string type, int index)
        {
            var response = await client.GetAsync(
                $"https://recruitment-test.investcloud.com/api/numbers/{dataset}/{type}/{index}")
                .ConfigureAwait(false);
            ResponseModel values = JsonConvert.DeserializeObject<ResponseModel>(await response.Content.ReadAsStringAsync())!;
            return values.Value;
        }

        public async Task<int[][]> GetWholeDataset(string dataset, string type, int size)
        {
            var index = Enumerable.Range(0, size-1);
            var tasks = index.Select(i => GetDatasetValue(dataset, type, i));
            var result = await Task.WhenAll(tasks);
            return result;
        }
        public async Task<int[][]> GetWholeDatasetBatches(string dataset, string type, int size)
        {
            var batchSize = 100;
            int numberOfBatches = (int)Math.Ceiling((double)size / batchSize);
            var result = new List<int[]>();

            for (int i = 0; i < numberOfBatches; i++)
            {
                var currentIndices = Enumerable.Range(i * batchSize, batchSize);
                var tasks = currentIndices.Select(index => GetDatasetValue(dataset, type, index));
                var batchResults = await Task.WhenAll(tasks);
                result.AddRange(batchResults);
            }

            return result.ToArray();;
        }
    }

    class ResponseModel
    {
        public int[] Value {get; set;} = Array.Empty<int>();
        public string? Cause {get; set;}
        public bool Success {get; set;}
    }
}

