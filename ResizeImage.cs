using System.Collections.Specialized;
using System.Net;
using System.Numerics;
using System.Web;
using HttpMultipartParser;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Boilerplate.Function
{
	public class ResizeImage
	{
		private readonly ILogger _logger;

		public ResizeImage(ILoggerFactory loggerFactory)
		{
			_logger = loggerFactory.CreateLogger<ResizeImage>();
		}

		[Function("ResizeImage")]
		public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
		{
			_logger.LogInformation("C# HTTP trigger function processed a request.");

			var parsedFormBody = await MultipartFormDataParser.ParseAsync(req.Body);


			var query = HttpUtility.ParseQueryString(req.Url.Query);

			var output = new MemoryStream();

			using (Image image = Image.Load(parsedFormBody.Files[0].Data, out IImageFormat format))
			{

				image.Mutate(x => x.AutoOrient());
				Size size = image.Size();
				int width = int.Parse(query["width"] ?? "0");
				int height = int.Parse(query["height"] ?? "0");

				if (query["cc"] != null && GetCropRectangle(image, query) is Rectangle rect)
				{
					// image.Mutate(x => x.Crop(rect));
					var opt = new ResizeOptions
					{
						TargetRectangle = rect,
						Size = new Size(width, height),
						Mode = ResizeMode.Min
					};

					image.Mutate(x => x.Resize(opt));

				}
				else if (query["rxy"] != null && GetFocusPoint(image, query) is PointF point)
				{
					var opt = new ResizeOptions
					{
						CenterCoordinates = point,
						Size = new Size(width, height),
						Mode = ResizeMode.Crop
					};

					image.Mutate(x => x.Resize(opt));
				}
				else if (width != 0 || height != 0)
				{
					image.Mutate(x => x.Resize(width, height));
				}

				image.Save(output, new JpegEncoder());
				output.Position = 0;
			}

			var response = req.CreateResponse(HttpStatusCode.OK);
			response.Headers.Add("Content-Type", "image/jpeg");
			response.Headers.Add("Content-Length", output.Length.ToString());
			response.Headers.Add("content-disposition", "attachment;filename=file.jpeg");
			response.WriteBytes(output.ToArray());

			return response;
		}

		private static Rectangle? GetCropRectangle(Image image, NameValueCollection query)
		{
			string[] parsed = query["cc"].Split(',');

			var coordinates = Array.ConvertAll(parsed, float.Parse);

			if (coordinates.Length != 4 ||
					(coordinates[0] == 0 && coordinates[1] == 0 && coordinates[2] == 0 && coordinates[3] == 0))
			{
				return null;
			}

			// The right and bottom values are actually the distance from those sides, so convert them into real coordinates and transform to correct orientation
			var left = Math.Clamp(coordinates[0], 0, 1);
			var top = Math.Clamp(coordinates[1], 0, 1);
			var right = Math.Clamp(1 - coordinates[2], 0, 1);
			var bottom = Math.Clamp(1 - coordinates[3], 0, 1);

			// Scale points to a pixel based rectangle
			Size size = image.Size();

			return Rectangle.Round(RectangleF.FromLTRB(
					left * size.Width,
					top * size.Height,
					right * size.Width,
					bottom * size.Height));
		}

		private static PointF? GetFocusPoint(Image image, NameValueCollection query)
		{
			string[] parsed = (query["rxy"]).Split(',');

			var coordinates = Array.ConvertAll(parsed, float.Parse);

			if (coordinates.Length != 2 ||
				(coordinates[0] == 0 && coordinates[1] == 0))
			{
				return null;
			}

			// The right and bottom values are actually the distance from those sides, so convert them into real coordinates and transform to correct orientation
			var left = Math.Clamp(coordinates[0], 0, 1);
			var top = Math.Clamp(coordinates[1], 0, 1);

			// Scale points to a pixel based rectangle
			return new PointF(left, top);
		}

	}
}
