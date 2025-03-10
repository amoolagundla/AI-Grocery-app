﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Interfaces
{
    public interface IBlobService
    {
        Task<(Stream Content, IDictionary<string, string> Metadata)> DownloadBlobWithMetadataAsync(string blobUrl);
    }
}
