using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UploadFileService.Model;

namespace UploadFileService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly ILogger<FileController> _logger;
        private readonly Config _config;
        private readonly KestrelSettings _kestrelSettings;

        public FileController(ILogger<FileController> logger, IOptions<Config> config, IOptions<KestrelSettings> kestrelSettings)
        {
            _logger = logger;
            _config = config.Value;
            _kestrelSettings = kestrelSettings.Value;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile()
        {
            try
            {
                if (Request.ContentLength == 0)
                {
                    return NoContent();
                }
                
                LogInformation($"config max size: {_config.maximum_file_size_in_MB}");
                LogInformation($"config path: {_config.tmp_path}");
                LogInformation($"config validity: {_config.validity_period_days}");

                var boundary = Request.ContentType.Split(';')[1].Split('=')[1].Trim();
                var reader = new MultipartReader(boundary, Request.Body);

                var section = await reader.ReadNextSectionAsync();
                if (section == null)
                    return BadRequest("File upload failed.");

                var contentDisposition = section.Headers["Content-Disposition"].ToString();
                var fileName = ParseFileName(contentDisposition);
                var extension = Path.GetExtension(fileName)?.ToLower() ?? string.Empty;
                if (string.IsNullOrEmpty(extension))
                {
                    extension = ".dat"; // Default extension if none is provided
                }
                string fileNameWoEx = Path.GetFileNameWithoutExtension(fileName);
                string uniqueFileName = "";

                if (string.IsNullOrWhiteSpace(fileNameWoEx))
                {
                    uniqueFileName = $"{Guid.NewGuid()}{extension}";
                }
                else
                {
                    var fileNameRemoveEmptySpace = Regex.Replace(Path.GetFileNameWithoutExtension(fileName), @"\s+", "");
                    uniqueFileName = $"{Guid.NewGuid()}_{fileNameRemoveEmptySpace}{extension}";
                }


                LogInformation($"File name: {uniqueFileName}");

                string filePath = Path.Combine(_config.tmp_path, uniqueFileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await section.Body.CopyToAsync(stream);
                }

                var fileInfo = new FileInfo(filePath);
                long fileSizeInMB = fileInfo.Length / (1024 * 1024);

                if (fileSizeInMB > _config.maximum_file_size_in_MB)
                {
                    LogError($"Uploaded file size exceeds configured limit: {_config.maximum_file_size_in_MB} MB.");
                    System.IO.File.Delete(filePath);
                    return BadRequest($"Uploaded file size exceeds configured limit: {_config.maximum_file_size_in_MB} MB.");
                }

                int portServer = _kestrelSettings.Endpoints.Http.PortServer;
                string baseUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Host}:{portServer}";
                string httpUrl = $"{baseUrl}/{uniqueFileName}";
                LogInformation($"Url: {httpUrl}");
                var xmlDoc = new System.Xml.XmlDocument();
                var fileNode = xmlDoc.CreateElement("file", "urn:gsma:params:xml:ns:rcs:rcs:fthttp");
                xmlDoc.AppendChild(fileNode);

                var fileInfoNode = xmlDoc.CreateElement("file-info");
                fileInfoNode.SetAttribute("type", "file");
                fileNode.AppendChild(fileInfoNode);

                var fileSizeNode = xmlDoc.CreateElement("file-size");
                fileSizeNode.InnerText = fileInfo.Length.ToString();
                fileInfoNode.AppendChild(fileSizeNode);

                var fileNameNode = xmlDoc.CreateElement("file-name");
                fileNameNode.InnerText = uniqueFileName;
                fileInfoNode.AppendChild(fileNameNode);

                var contentTypeNode = xmlDoc.CreateElement("content-type");
                contentTypeNode.InnerText = "application/octet-stream";
                fileInfoNode.AppendChild(contentTypeNode);

                var dataNode = xmlDoc.CreateElement("data");
                dataNode.SetAttribute("url", httpUrl);
                dataNode.SetAttribute("until", DateTime.UtcNow.AddDays(_config.validity_period_days).ToString("yyyy-MM-ddTHH:mm:ssZ"));
                fileInfoNode.AppendChild(dataNode);

                return Content(xmlDoc.OuterXml, "application/xml");
            }
            catch (Exception ex)
            {
                LogError($"Error processing file upload: {ex.ToString()}");
                return StatusCode(500, "Internal server error.");
            }
        }

        private void LogError(string message)
        {
            _logger.LogError(message);
        }

        private void LogInformation(string message)
        {
            _logger.LogInformation(message);
        }

        private string ParseFileName(string contentDisposition)
        {
            var fileName = string.Empty;
            var segments = contentDisposition.Split(';');
            foreach (var segment in segments)
            {
                if (segment.Trim().StartsWith("filename=", StringComparison.OrdinalIgnoreCase))
                {
                    fileName = segment.Split('=')[1].Trim('"').Trim();
                    break;
                }
            }
            return fileName;
        }

    }
}