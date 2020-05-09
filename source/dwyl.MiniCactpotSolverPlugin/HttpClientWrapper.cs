using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace dwyl.MiniCactpotSolverPlugin
{
    public class HttpClientWrapper : IDisposable
    {
        private static readonly HttpClient HttpClient;

        static HttpClientWrapper()
        {
            HttpClient = new HttpClient();
        }

        public async Task<(HttpStatusCode statusCode, byte[] value)> PostAsync(Uri uri, ByteArrayContent byteArrayContent, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = uri,
                Content = byteArrayContent
            };

            var response = await HttpClient.SendAsync(request, cancellationToken);

            return response.IsSuccessStatusCode ? (response.StatusCode, await response.Content.ReadAsByteArrayAsync()) : (response.StatusCode, null);
        }


        #region IDisposable Support
        private bool _disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue)
            {
                return;
            }

            if (disposing)
            {
                HttpClient.Dispose();
            }

            this._disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

    }

}
