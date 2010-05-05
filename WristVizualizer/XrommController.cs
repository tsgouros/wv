using System;
using System.Collections.Generic;
using System.Text;
using libWrist;
using libCoin3D;

namespace WristVizualizer
{
    class XrommController : Controller
    {
        private XrommFilesystem _xrommFileSys;
        private Separator _root;

        public XrommController(string filename)
        {
            _root = new Separator();
            _xrommFileSys = new XrommFilesystem(filename);
            LoadXromm();
            readAllFiles();
        }

        private void LoadXromm()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override Separator Root
        {
            get { return _root; }
        }

        private void readAllFiles()
        {
            foreach (string ivFile in _xrommFileSys.bpaths)
                _root.addFile(ivFile);
        }


        //public override string ApplicationTitle { get { return _firstFilename; } }
        //public override string WatchedFileFilename { get { return _firstFilename; } }
        public override string LastFileFilename { get { return _xrommFileSys.PathFirstIVFile; } }
    }
}