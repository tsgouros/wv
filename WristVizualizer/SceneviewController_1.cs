using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using libWrist;
using libCoin3D;

namespace WristVizualizer
{
    class SceneviewController : Controller
    {
        private Separator _root;
        private string _firstFilename = null;

        public SceneviewController(string[] files)
        {
            _root = new Separator();
            ImportFilesToScene(files);
        }

        //set options
        public override string ApplicationTitle { get { return _firstFilename; } }
        public override string WatchedFileFilename { get { return _firstFilename; } }
        public override string LastFileFilename { get { return _firstFilename; } }
        public override bool CanImportObject { get { return true; } }
        public override bool CanViewSource { get { return true; } }

        public override void ImportFilesToScene(string[] filenames)
        {
            foreach (string filename in filenames)
                ImportFilesToScene(filename);
        }

        private void ImportFilesToScene(string filename)
        {
            string ext = Path.GetExtension(filename).ToLower();
            switch (ext)
            {
                case ".iv":
                case ".vrml":
                case ".wrl":
                    _root.addFile(filename);
                    break;
                case ".stack":
                case ".dat":
                    _root.addChild(readStackfile(filename));
                    break;
                default:
                    throw new WristException("Error: Unknown file extension for: " + filename);
            }

            //save the name of the first file!
            if (_firstFilename == null)
                _firstFilename = filename;
        }

        private Separator readStackfile(string filename)
        {
            double[][] pts = DatParser.parseDatFile(filename);
            return Texture.createPointsFileObject(pts);
        }

        public override Separator Root
        {
            get { return _root; }
        }
    }
}
