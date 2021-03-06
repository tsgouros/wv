#include "StdAfx.h"
#include "ExaminerViewer.h"

 // remove me later
#include <Inventor/nodes/SoCone.h>      // remove me later
#include <Inventor/nodes/SoSphere.h>      // remove me later
#include <Inventor/nodes/SoPerspectiveCamera.h>
#include <Inventor/nodes/SoOrthographicCamera.h>


//for output
#include <Inventor/SoOffscreenRenderer.h>
#include <Inventor/actions/SoGLRenderAction.h>
#include <Inventor/actions/SoWriteAction.h>

//for raypick
#include <Inventor/events/SoMouseButtonEvent.h>
#include <Inventor/actions/SoRayPickAction.h>
#include <Inventor/SoPickedPoint.h>

#include <Inventor/actions/SoLineHighlightRenderAction.h>
#include <Inventor/actions/SoBoxHighlightRenderAction.h>

//for selecting and material editing
#include <Inventor/actions/SoSearchAction.h>
#include <Inventor/actions/SoCallbackAction.h>


#include "MyViewport.h"

static void event_cb(void * ud, SoEventCallback * n);
static void event_selected_cb( void * userdata, SoPath * path );
static void event_deselected_cb( void * userdata, SoPath * path );

libCoin3D::ExaminerViewer::ExaminerViewer(int parrent)
{
	_viewer = NULL;
	_ecb = NULL;
	_decorated = TRUE;
	_myOffscreenRenderer = NULL;
	_viewer = new SoWinExaminerViewer((HWND)parrent);
	Parent_HWND = parrent;

	_root = new SoSeparator; // remove me later
    _viewer->setSceneGraph(_root);         // remove me later
	_viewer->setCameraType(SoOrthographicCamera::getClassTypeId());

	//add reference of this Viewer to the table, so we can get it if needed
	ViewersHashtable->Add(parrent,this);
}

libCoin3D::ExaminerViewer::~ExaminerViewer()
{
	if (_viewer != NULL)
		delete _viewer;
	if (_myOffscreenRenderer != NULL)
		delete _myOffscreenRenderer;
}

void libCoin3D::ExaminerViewer::setDecorator(bool decorate)
{
	if (_viewer != NULL && _decorated!=decorate) {
		_decorated = decorate;
		_viewer->setDecoration(decorate);
	}
}

void libCoin3D::ExaminerViewer::setDrawStyle()
{
	_viewer->setDrawStyle(SoWinViewer::STILL, SoWinViewer::VIEW_LOW_COMPLEXITY);
	_viewer->setDrawStyle(SoWinViewer::STILL, SoWinViewer::VIEW_AS_IS);
}

void libCoin3D::ExaminerViewer::viewAll()
{
	_viewer->viewAll(); //move the camera so the whole scene can be viewed
}

void libCoin3D::ExaminerViewer::setSceneGraph(Separator^ root)
{
	_root = root->getSoSeparator();
	_selection = new SoSelection();
	_selection->ref();
	_selection->policy = SoSelection::SINGLE;

	_selection->addSelectionCallback( event_selected_cb, _viewer );
	_selection->addDeselectionCallback( event_deselected_cb, _viewer );

	_selection->addChild(root->getSoSeparator());
	if (_viewer != NULL)
		_viewer->setSceneGraph(_selection);
		//_viewer->setSceneGraph(root->getSoSeparator());

	//_viewer->setGLRenderAction( new SoLineHighlightRenderAction );
	//_viewer->setGLRenderAction( new SoBoxHighlightRenderAction );
	_selection->unref(); //should be ref by _viewer

	OnNewSceneGraphLoaded(); //raise an event for those listening
}

void libCoin3D::ExaminerViewer::saveSceneGraph(System::String^ filename)
{
	char* fname = (char*)System::Runtime::InteropServices::Marshal::StringToHGlobalAnsi(filename).ToPointer();
	//check if the root exists
	SoWriteAction wa;
	SbBool success = wa.getOutput()->openFile(fname);
	if (success==FALSE)
		throw gcnew System::ArgumentException("Unable to open filename for writing: "+filename);

	wa.getOutput()->setBinary(FALSE); //save in ASCII format
	wa.apply(_root);  //TODO: Fix so we don't save the first SoSeparator and SoDrawStyle
	wa.getOutput()->closeFile();
}

libCoin3D::Camera^ libCoin3D::ExaminerViewer::Camera::get()
{
	return gcnew libCoin3D::Camera(_viewer->getCamera());
}

bool libCoin3D::ExaminerViewer::saveToJPEG(System::String ^filename)
{
	return saveToImage(filename,"jpg");
}
bool libCoin3D::ExaminerViewer::saveToPNG(System::String ^filename)
{
	return saveToImage(filename,"png");
}
bool libCoin3D::ExaminerViewer::saveToGIF(System::String ^filename)
{
	return saveToImage(filename,"gif");
}
bool libCoin3D::ExaminerViewer::saveToTIFF(System::String ^filename)
{
	//TODO: Fix error with saveing screenshots to other formats - include simage.lib?
	//test Code....don't know why I can't load these other types...
	/*
	SbPList extlist;
    SbString fullname, description;
	SoOffscreenRenderer * r = new SoOffscreenRenderer(*(new SbViewportRegion));
	int num = r->getNumWriteFiletypes();
	for (int i=0; i<num; i++) {
    r->getWriteFiletypeInfo(i, extlist, fullname, description);
	fullname += ": ";
	fullname += description;
	System::Console::Write(fullname.getString());
    //(void)fprintf(stdout, "%s: %s (extension%s: ",
      //                fullname.getString(), description.getString(),
        //              extlist.getLength() > 1 ? "s" : "");
        for (int j=0; j < extlist.getLength(); j++) {
			System::Console::Write((const char*) extlist[j]);
          //(void)fprintf(stdout, "%s%s", j>0 ? ", " : "", (const char*) extlist[j]);
        }
		System::Console::WriteLine("");
   //(void)fprintf(stdout, ")\n");
	}
	delete r;
	*/
	return saveToImage(filename,"tiff");
}
bool libCoin3D::ExaminerViewer::saveToBMP(System::String ^filename)
{
	return saveToImage(filename,"bmp");
}

//System::Drawing::Bitmap^ libCoin3D::ExaminerViewer::getSmoothedImage(int smoothFactor)
//{
//	System::Drawing::Image^ rawImage = getImage();
//	if (smoothFactor == 1)
//		return (System::Drawing::Bitmap^)rawImage;
//
//	System::Drawing::Image^ finalImage = gcnew System::Drawing::Bitmap(rawImage->Size.Width / smoothFactor, rawImage->Size.Height / smoothFactor);
//	System::Drawing::Graphics^ g = System::Drawing::Graphics::FromImage(finalImage);
//	g->InterpolationMode = System::Drawing::Drawing2D::InterpolationMode::HighQualityBicubic;
//	g->DrawImage(rawImage, 0, 0, rawImage->Size.Width / smoothFactor, rawImage->Size.Height / smoothFactor);
//	delete g;
//	delete rawImage;
//	return (System::Drawing::Bitmap^)finalImage;
//}

System::Drawing::Image^ libCoin3D::ExaminerViewer::getImage()
{
	SoOffscreenRenderer *myRenderer = getOffscreenRenderer();

    if (!myRenderer->render(_viewer->getSceneManager()->getSceneGraph())) {  //render root?
		disposeOfTemporaryRenderer(myRenderer);
		System::Console::WriteLine("Couldn't capture root of tree");
		return nullptr;
    }

	//get the pixel size of the new image
	unsigned char* buffer = myRenderer->getBuffer();
	short x, y;
	myRenderer->getViewportRegion().getViewportSizePixels().getValue(x,y);

	//create a new image that we will later return
	System::Drawing::Bitmap^ im = gcnew System::Drawing::Bitmap(x,y,
		System::Drawing::Imaging::PixelFormat::Format24bppRgb);
	//need to initialize the graphic, don't know why
	System::Drawing::Graphics^ g = System::Drawing::Graphics::FromImage(im);
	g->FillRectangle(System::Drawing::Brushes::White,0,0,x,y);

	//setup BitmapData for raw editing
	System::Drawing::Imaging::BitmapData^ bitdata;
	System::Drawing::Rectangle rect = System::Drawing::Rectangle(0,0,x,y);
	bitdata = im->LockBits(rect,
		System::Drawing::Imaging::ImageLockMode::ReadWrite,
		im->PixelFormat);

	//need to fix the rgb data
	int bytesInBuffer = im->Width*im->Height*3;
	array<System::Byte>^ rgbValues = gcnew array<System::Byte>(bytesInBuffer);
	//copy all the buffer data into the array
	System::Runtime::InteropServices::Marshal::Copy((System::IntPtr)buffer,rgbValues,0,bytesInBuffer);

	//for some reason, 24bppRgb actually returns (and therefore requires) BGR data, not RGB, so we need to flip a lot....fuck
	System::Byte tempByte;
	for (int i=0; i<bytesInBuffer; i += 3) {
		tempByte = rgbValues[i];
		rgbValues[i] = rgbValues[i+2];
		rgbValues[i+2] = tempByte;
	}
	
	//check that the stride length is what we think
	if (bitdata->Stride == im->Width*3) {
		//if its exact, then we can copy in one go
		int bytes = bitdata->Stride*im->Height;
		System::Runtime::InteropServices::Marshal::Copy(rgbValues,0,bitdata->Scan0,bytes);
	}
	else {
		//the stride is wrong, so we need to copy each line by itself
		int bytesPerLine = im->Width*3;
		for (int i=0; i<im->Height; i++) {
			//copy to the right location, need to make our offset based on the stride size
			System::IntPtr destination = System::IntPtr(bitdata->Scan0.ToInt64() + i*bitdata->Stride);
			System::Runtime::InteropServices::Marshal::Copy(rgbValues,i*bytesPerLine,destination,bytesPerLine); 
		}
	}

	im->UnlockBits(bitdata);

	//flip us over
	im->RotateFlip(System::Drawing::RotateFlipType::RotateNoneFlipY);

    disposeOfTemporaryRenderer(myRenderer);
	return im;
}

bool libCoin3D::ExaminerViewer::saveToImage(System::String ^filename, char *ext) 
{
	char* fname = (char*)System::Runtime::InteropServices::Marshal::StringToHGlobalAnsi(filename).ToPointer();

	SoOffscreenRenderer *myRenderer = getOffscreenRenderer();

    if (!myRenderer->render(_viewer->getSceneManager()->getSceneGraph())) {  //render root?
		disposeOfTemporaryRenderer(myRenderer);
		System::Console::WriteLine("Couldn't capture root of tree");
		return false;
    }

    // Generate PostScript and write it to the given file
	bool result = (myRenderer->writeToFile(fname, ext) != 0);

    disposeOfTemporaryRenderer(myRenderer);
	System::Runtime::InteropServices::Marshal::FreeHGlobal((System::IntPtr)fname);

	return result;
}

SoOffscreenRenderer* libCoin3D::ExaminerViewer::getOffscreenRenderer()
{
	return getOffscreenRenderer(1);
}

SoOffscreenRenderer* libCoin3D::ExaminerViewer::getOffscreenRenderer(int scaleFactor)
{
	if (_myOffscreenRenderer != NULL)
		return _myOffscreenRenderer;

	const SbViewportRegion &vp  = _viewer->getViewportRegion();
	const SbVec2s &oldImagePixSize = vp.getViewportSizePixels();
	SbVec2s imagePixSize = SbVec2s(oldImagePixSize);

	//make sure that our image dimensions are a multiple of 4, needed for video,
	//not certain why its leaving blue borders on the other images though....
	imagePixSize[0] += imagePixSize[0] % 4;
	imagePixSize[1] += imagePixSize[1] % 4;

	//now scale that image, by the appropriate factor
	imagePixSize *= scaleFactor;


    // Create a viewport to render the scene into.
    SbViewportRegion myViewport;
    myViewport.setWindowSize(imagePixSize);

	// Render the scene
	SoGLRenderAction *newRA = new SoGLRenderAction(myViewport);
	newRA->setTransparencyType(_viewer->getGLRenderAction()->getTransparencyType());    
	SoOffscreenRenderer *myRenderer = new SoOffscreenRenderer(newRA);
	myRenderer->setBackgroundColor(_viewer->getBackgroundColor());

	return myRenderer;
}

void libCoin3D::ExaminerViewer::disposeOfTemporaryRenderer(SoOffscreenRenderer* renderer)
{
	//only dispose if there is no cached renderer
	if (_myOffscreenRenderer == NULL)
		delete renderer;
}

void libCoin3D::ExaminerViewer::clearOffscreenRenderer()
{
	if (_myOffscreenRenderer != NULL)
		delete _myOffscreenRenderer;
	_myOffscreenRenderer = NULL; //make sure that we remove this reference
}

void libCoin3D::ExaminerViewer::cacheOffscreenRenderer()
{
	cacheOffscreenRenderer(1);
}

void libCoin3D::ExaminerViewer::cacheOffscreenRenderer(int scaleFactor)
{
	clearOffscreenRenderer();
	_myOffscreenRenderer = getOffscreenRenderer(scaleFactor);
}

float libCoin3D::ExaminerViewer::getBackgroundColorR()
{
	return _viewer->getBackgroundColor()[0];
}
float libCoin3D::ExaminerViewer::getBackgroundColorG()
{
	return _viewer->getBackgroundColor()[1];
}
float libCoin3D::ExaminerViewer::getBackgroundColorB()
{
	return _viewer->getBackgroundColor()[2];
}

int libCoin3D::ExaminerViewer::getBackgroundColor()
{
	return _viewer->getBackgroundColor().getPackedValue();
}

void libCoin3D::ExaminerViewer::setBackgroundColor(float r, float g, float b)
{
	_viewer->setBackgroundColor(SbColor(r,g,b));
}

void libCoin3D::ExaminerViewer::setBackgroundColor(int rgb) 
{
	SbColor c;
	float junk=0;
	c.setPackedValue(rgb,junk);
	_viewer->setBackgroundColor(c);
}

void libCoin3D::ExaminerViewer::setFeedbackVisibility(bool visible)
{
	//only change, if its different
	if ((_viewer->isFeedbackVisible()!=0) != visible)
		_viewer->setFeedbackVisibility(visible);

}

void libCoin3D::ExaminerViewer::setRaypick()
{
	if (_ecb != NULL) //then we already have a callback set
		return;

	_ecb = new SoEventCallback;
	_ecb->addEventCallback(SoMouseButtonEvent::getClassTypeId(), event_cb, _viewer);
	_root->insertChild(_ecb,0); //insert us right at the top, baby! we hear it all!
}

void libCoin3D::ExaminerViewer::resetRaypick()
{
	if (_ecb == NULL) //nothing to do
		return;

	_root->removeChild(_ecb); //remove from tree
	_ecb=NULL; //remove reference
}

void libCoin3D::ExaminerViewer::fireClick(float x, float y, float z)
{
	OnRaypick(x,y,z);
}

libCoin3D::ExaminerViewer^ libCoin3D::ExaminerViewer::getViewerByParentWidget(int HWND)
{
	if (ViewersHashtable == nullptr)
		return nullptr; //this should never happen, but just in case
	return (ExaminerViewer^)ViewersHashtable[HWND]; //return it
}

libCoin3D::Material^ libCoin3D::ExaminerViewer::getSelectedMaterial()
{
	if (_selection==NULL || _selection->getNumSelected()==0)
		return nullptr;

	//okay, need to find the material, no idea how
	SoMaterial* result = getMaterialForSelectedNode();
	if (result==NULL)
		return nullptr;

	//else we are good, sent it back
	return gcnew Material(result);
}

libCoin3D::Material^ libCoin3D::ExaminerViewer::createMaterialForSelected()
{
	if (_selection==NULL || _selection->getNumSelected()==0)
		return nullptr;

	SoMaterial* result = createMaterialForSelectedNode();
	if (result==NULL)
		return nullptr;

	//else we are good, send it back
	return gcnew Material(result);
}

//void libCoin3D::ExaminerViewer::enableSelection()
//{
//}
//
void libCoin3D::ExaminerViewer::disableSelection()
{
	if (_selection==NULL)
		return;

	_selection->ref();
	_viewer->setSceneGraph(_root);
	_selection->removeChild(_root);
	_selection->unref();
	_selection=NULL; //remove reference
}

libCoin3D::ExaminerViewer::TransparencyTypes libCoin3D::ExaminerViewer::getTransparencyType()
{
	SoGLRenderAction::TransparencyType current = _viewer->getGLRenderAction()->getTransparencyType();
	switch (current) 
	{
	case SoGLRenderAction::ADD:
		return TransparencyTypes::ADD;
	case SoGLRenderAction::BLEND:
		return TransparencyTypes::BLEND;
	case SoGLRenderAction::DELAYED_ADD:
		return TransparencyTypes::DELAYED_ADD;
	case SoGLRenderAction::DELAYED_BLEND:
		return TransparencyTypes::DELAYED_BLEND;
	case SoGLRenderAction::SCREEN_DOOR:
		return TransparencyTypes::SCREEN_DOOR;
	case SoGLRenderAction::SORTED_OBJECT_ADD:
		return TransparencyTypes::SORTED_OBJECT_ADD;
	case SoGLRenderAction::SORTED_OBJECT_BLEND:
		return TransparencyTypes::SORTED_OBJECT_BLEND;
	case SoGLRenderAction::SORTED_OBJECT_SORTED_TRIANGLE_ADD:
		return TransparencyTypes::SORTED_OBJECT_SORTED_TRIANGLE_ADD;
	case SoGLRenderAction::SORTED_OBJECT_SORTED_TRIANGLE_BLEND:
		return TransparencyTypes::SORTED_OBJECT_SORTED_TRIANGLE_BLEND;
	default:
		return TransparencyTypes::SCREEN_DOOR;
	}
}

void libCoin3D::ExaminerViewer::setTransparencyType(TransparencyTypes type)
{

	SoGLRenderAction::TransparencyType newType;
	switch (type) 
	{
	case TransparencyTypes::ADD:
		newType = SoGLRenderAction::ADD; 
		break;
	case TransparencyTypes::BLEND:
		newType = SoGLRenderAction::BLEND; 
		break;
	case TransparencyTypes::DELAYED_ADD:
		newType = SoGLRenderAction::DELAYED_ADD; 
		break;
	case TransparencyTypes::DELAYED_BLEND:
		newType = SoGLRenderAction::DELAYED_BLEND; 
		break;
	case TransparencyTypes::SCREEN_DOOR:
		newType = SoGLRenderAction::SCREEN_DOOR; 
		break;
	case TransparencyTypes::SORTED_OBJECT_ADD:
		newType = SoGLRenderAction::SORTED_OBJECT_ADD; 
		break;
	case TransparencyTypes::SORTED_OBJECT_BLEND:
		newType = SoGLRenderAction::SORTED_OBJECT_BLEND; 
		break;
	case TransparencyTypes::SORTED_OBJECT_SORTED_TRIANGLE_ADD:
		newType = SoGLRenderAction::SORTED_OBJECT_SORTED_TRIANGLE_ADD; 
		break;
	case TransparencyTypes::SORTED_OBJECT_SORTED_TRIANGLE_BLEND:
		newType = SoGLRenderAction::SORTED_OBJECT_SORTED_TRIANGLE_BLEND; 
		break;
	default:
		newType = SoGLRenderAction::SCREEN_DOOR;
		break;
	}
	_viewer->getGLRenderAction()->setTransparencyType(newType);
}

libCoin3D::ExaminerViewer::HighlighRenderTypes libCoin3D::ExaminerViewer::getHighlightType()
{
	if (_viewer->getGLRenderAction()->getTypeId() == SoBoxHighlightRenderAction::getClassTypeId()) {
		return HighlighRenderTypes::BOX_HIGHLIGHT_RENDER;
	}
	else if (_viewer->getGLRenderAction()->getTypeId() == SoLineHighlightRenderAction::getClassTypeId()) {
		return HighlighRenderTypes::LINE_HIGHLIGHT_RENDER;
	}
	else {
		const char* typeName = _viewer->getGLRenderAction()->getClassTypeId().getName().getString();
		throw gcnew System::Exception(System::String::Format("Unknown SoGLRenderAction found for viewer. ({0})", gcnew System::String(typeName)));
	}
}

void libCoin3D::ExaminerViewer::setHighlightType(libCoin3D::ExaminerViewer::HighlighRenderTypes type)
{
	//create a new Render action of the correct type
	SoGLRenderAction* newRenderer;
	switch (type) {
		case HighlighRenderTypes::BOX_HIGHLIGHT_RENDER:
			newRenderer = new SoBoxHighlightRenderAction();
			break;
		case HighlighRenderTypes::LINE_HIGHLIGHT_RENDER:
			newRenderer = new SoLineHighlightRenderAction();
			break;
		default:
			throw gcnew System::ArgumentException("Invalid HighlightRenderType");
	}

	//now lets copy over the old settings (or try to)
	SoGLRenderAction* oldRenderer = _viewer->getGLRenderAction();
	newRenderer->setTransparencyType(oldRenderer->getTransparencyType());
	newRenderer->setViewportRegion(oldRenderer->getViewportRegion());
	newRenderer->setSortedLayersNumPasses(oldRenderer->getSortedLayersNumPasses());

	//now lets introduce us into the the scene
	_viewer->setGLRenderAction(newRenderer);
}

void libCoin3D::ExaminerViewer::fireChangeObjectSelection(bool selected)
{
	if (selected)
		OnObjectSelected();
	else
		OnObjectDeselected();
}


struct SearchData {
	SoNode* searchNode;
	SoMaterial* resultingMaterial;
};

SoCallbackAction::Response precallback_action_cb(void *userdata, SoCallbackAction *action, const SoNode *node)
{
	SearchData* data = (SearchData*)userdata;
	SoNode* searchnode = data->searchNode;
	if (searchnode==node) {
		SbColor ambient;
		SbColor diffuse;
		SbColor specular;
		SbColor emission;
		float shininess;
		float transparency;
		action->getMaterial(ambient, diffuse, specular, emission, shininess, transparency);
		data->resultingMaterial = new SoMaterial();
		data->resultingMaterial->ref(); //make sure we don't disapear
		data->resultingMaterial->ambientColor.setValue(ambient);
		data->resultingMaterial->diffuseColor.setValue(diffuse);
		data->resultingMaterial->specularColor.setValue(specular);
		data->resultingMaterial->emissiveColor.setValue(emission);
		data->resultingMaterial->shininess.setValue(shininess);
		data->resultingMaterial->transparency.setValue(transparency);
		return SoCallbackAction::ABORT;
	}
	return SoCallbackAction::CONTINUE;
}

SoMaterial* libCoin3D::ExaminerViewer::getMaterialPropertiesAtNode(SoNode* node)
{
	SearchData data;
	data.resultingMaterial = NULL;
	data.searchNode = node;

	SoCallbackAction ca;
	ca.addPreCallback(node->getTypeId(),precallback_action_cb,&data);
	ca.apply(_root);
	return data.resultingMaterial;
}

libCoin3D::Separator^ libCoin3D::ExaminerViewer::getSeparatorForSelection()
{
	if (_selection==NULL || _selection->getNumSelected()!=1)
		return nullptr;

	//try and get an SoSeparator 1 above, if not, then work our way up the path
	for (int i=0; i< _selection->getPath(0)->getLength() - 1; i++) {
		if (_selection->getPath(0)->getNodeFromTail(i)->isOfType(SoSeparator::getClassTypeId()))
			return gcnew Separator((SoSeparator*)_selection->getPath(0)->getNodeFromTail(i));
	}

	//no luck at all, oh well
	return nullptr;
}

libCoin3D::Separator^ libCoin3D::ExaminerViewer::getSecondSeparatorForSelection()
{
	if (_selection==NULL || _selection->getNumSelected()!=1)
		return nullptr;

	//try and get an SoSeparator 2 above, if not, 1 above....I think
	if (_selection->getPath(0)->getLength() >= 3
		&& _selection->getPath(0)->getNodeFromTail(2)->isOfType(SoSeparator::getClassTypeId())) {
			//found one two up :)
			return gcnew Separator((SoSeparator*)_selection->getPath(0)->getNodeFromTail(2));
	}

	//okay, lets try 1 above
	if (_selection->getPath(0)->getLength() >= 2
		&& _selection->getPath(0)->getNodeFromTail(1)->isOfType(SoSeparator::getClassTypeId())) {
			//found one two up :)
			return gcnew Separator((SoSeparator*)_selection->getPath(0)->getNodeFromTail(1));
	}

	//okay, no luck still, lets work our way up the tree
	for (int i=3; i< _selection->getPath(0)->getLength() - 1; i++) {
		if (_selection->getPath(0)->getNodeFromTail(i)->isOfType(SoSeparator::getClassTypeId()))
			return gcnew Separator((SoSeparator*)_selection->getPath(0)->getNodeFromTail(i));
	}

	//no luck at all, oh well
	return nullptr;
}


SoNode* libCoin3D::ExaminerViewer::getSelectedNode()
{
	if (_selection->getNumSelected()!=1) //only want to deal with one
		return NULL;

	return _selection->getPath(0)->getTail();
}

SoGroup* libCoin3D::ExaminerViewer::getParentOfNode(SoNode* child)
{
	SoSearchAction sa;
	sa.setNode(child);
	sa.setInterest(SoSearchAction::ALL);
	sa.apply(_root);
	SoPathList pl = sa.getPaths();
	if (pl.getLength()!=1) {
		fprintf(stderr,"Problem!\n"); //TODO: More errors!
		return NULL;
	}
	SoPath* myPath = pl[0];
	if (myPath->getLength() < 2) {
		fprintf(stderr,"Problem, no parrent!\n"); //TODO: More errors!
		return NULL;
	}
	SoGroup* parent = (SoGroup*)myPath->getNodeFromTail(1);
	return parent;
}

SoGroup* libCoin3D::ExaminerViewer::getParentOfSelectedNode()
{
	if (_selection->getPath(0)->getLength() < 2) {
		fprintf(stderr,"Problem, no parrent!\n"); //TODO: More errors!
		return NULL;
	}
	return (SoGroup*)_selection->getPath(0)->getNodeFromTail(1);
}

SoMaterial* libCoin3D::ExaminerViewer::getMaterialForSelectedNode()
{
	SoNode* selectedNode = getSelectedNode();
	SoGroup* parentNode = getParentOfSelectedNode();
	if (selectedNode==NULL || parentNode==NULL)
		return NULL; //error TODO: something

	int childIndex = parentNode->findChild(selectedNode);
	if (childIndex==0) //child is first, can't have a material
		return NULL;

	SoNode* previousNode = parentNode->getChild(childIndex-1);
	if (previousNode->isOfType(SoMaterial::getClassTypeId())) {
		//hey We found a material Node!!!!
		return (SoMaterial*)previousNode;
	}
	return NULL;  //no luck
}

SoMaterial* libCoin3D::ExaminerViewer::createMaterialForSelectedNode()
{
	SoNode* selectedNode = getSelectedNode();
	SoGroup* parentNode = getParentOfSelectedNode();
	if (selectedNode==NULL || parentNode==NULL)
		return NULL; //error TODO: somethingng

	int childIndex = parentNode->findChild(selectedNode);
	//inherit existing color properties
	SoMaterial* myMaterial = getMaterialPropertiesAtNode(selectedNode); 
	if (myMaterial==NULL)
		myMaterial = new SoMaterial();

	parentNode->insertChild(myMaterial,childIndex); //insert right before the child
	return myMaterial;
}

void libCoin3D::ExaminerViewer::removeMaterialFromScene(Material^ material)
{
	SoNode* nodeToRemove = material->getNode();
	SoGroup* parent = getParentOfNode(nodeToRemove);
	if (nodeToRemove==NULL || parent==NULL) return; //lets try and be safe, yes we bury errors, oh well
	parent->removeChild(nodeToRemove);
}

void libCoin3D::ExaminerViewer::setSelection(ScenegraphNode^ node)
{
	if (_selection == NULL)
		return;

	_selection->deselectAll();
	_selection->select(node->getNode());
	_selection->touch();
}

static void event_cb(void * ud, SoEventCallback * n)
{
	const SoMouseButtonEvent * mbe = (SoMouseButtonEvent *)n->getEvent();

	if (mbe->getButton() != SoMouseButtonEvent::BUTTON1 ||
      mbe->getState() != SoButtonEvent::DOWN)
	  return;

	SoWinExaminerViewer * viewer = (SoWinExaminerViewer *)ud;
    SoRayPickAction rp(viewer->getViewportRegion());
	rp.setPoint(mbe->getPosition());
    rp.apply(viewer->getSceneManager()->getSceneGraph());

    SoPickedPoint * point = rp.getPickedPoint();
    if (point == NULL) {
		return;
    }

    n->setHandled();

    //(void)fprintf(stdout, "\n");

    SbVec3f v = point->getPoint();

	//triger event
	libCoin3D::ExaminerViewer^ realViewer = libCoin3D::ExaminerViewer::getViewerByParentWidget((int)viewer->getParentWidget());
	realViewer->fireClick(v[0],v[1],v[2]);
}

static void event_selected_cb( void * userdata, SoPath * path )
{
	static SbBool lock = FALSE;
	// Avoid processing recursive calls when we explicitly
	// select/deselect paths in the toplevel SoSelection node.
	if (lock) return;
	lock = TRUE;

	SoWinExaminerViewer * viewer = (SoWinExaminerViewer *)userdata;
	libCoin3D::ExaminerViewer^ realViewer = libCoin3D::ExaminerViewer::getViewerByParentWidget((int)viewer->getParentWidget());
	realViewer->_selection->touch();  //force redraw of the scene
	realViewer->fireChangeObjectSelection(true);

	//While not 100% accurate, this fixes a problem. Really, the event is only trying to say when there
	//is 1 object selected (so we can edit it). If we have more, then we actually want to say that there
	//is nothing selected, to prevent trying to edit the material of multiple objects :)
	//if (realViewer->_selection->getNumSelected() != 1)
	if (path->getTail()->isOfType(SoGroup::getClassTypeId()))
		realViewer->fireChangeObjectSelection(false);

	lock = FALSE;
}

static void event_deselected_cb( void * userdata, SoPath * path )
{
	static SbBool lock = FALSE;
	// Avoid processing recursive calls when we explicitly
	// select/deselect paths in the toplevel SoSelection node.
	if (lock) return;
	lock = TRUE;

	SoWinExaminerViewer * viewer = (SoWinExaminerViewer *)userdata;
	libCoin3D::ExaminerViewer^ realViewer = libCoin3D::ExaminerViewer::getViewerByParentWidget((int)viewer->getParentWidget());
	realViewer->_selection->touch(); //force redraw of the scene
	realViewer->fireChangeObjectSelection(false);

	lock = FALSE;
}