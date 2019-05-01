using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourseWork
{
    public abstract class Message
    {
        public DownloadingFile targetFile;
        public PeerConnection targetConnection;
    }
}
