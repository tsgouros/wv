#include "StdAfx.h"
#include "Transform.h"



libCoin3D::Transform::Transform(void)
{
	_transform = new SoTransform();
	_transform->ref();
	_rotMatrix = NULL;
}

libCoin3D::Transform::~Transform()
{
	if (_rotMatrix==NULL)
		delete _rotMatrix;

	_transform->unref();
}

void
libCoin3D::Transform::setRotation(double r00, double r01, double r02, double r10, double r11, double r12, double r20, double r21, double r22)
{
	SbMatrix R, r1;
	R.makeIdentity();
	/*
	R[0][3] = 0; R[1][3] = 0; R[2][3] = 0;
	R[3][0] = 0; R[3][1] = 0; R[3][2] = 0;
	R[3][3] = 1.0; 
	*/

	R[0][0] = (float)r00;
	R[0][1] = (float)r01;
	R[0][2] = (float)r02;
	R[1][0] = (float)r10;
	R[1][1] = (float)r11;
	R[1][2] = (float)r12;
	R[2][0] = (float)r20;
	R[2][1] = (float)r21;
	R[2][2] = (float)r22;

	

	SbRotation tempR;
	tempR.setValue(R.transpose());
	_transform->rotation.setValue(tempR);
	_rotMatrix = new SbMatrix[1];
	_rotMatrix[0] = R.transpose();
	
}

void libCoin3D::Transform::setTranslation(double v0, double v1, double v2) 
{
	float T[3];
	T[0] = (float)v0;
	T[1] = (float)v1;
	T[2] = (float)v2;
	_transform->translation.setValue(T);
}

void libCoin3D::Transform::setTransform(double r00,double r01,double r02,double r10,double r11,double r12,double r20,double r21,double r22,double v0, double v1, double v2)
{
		SbMatrix R, r1;
	R.makeIdentity();

	R[0][0] = (float)r00;
	R[0][1] = (float)r01;
	R[0][2] = (float)r02;
	R[1][0] = (float)r10;
	R[1][1] = (float)r11;
	R[1][2] = (float)r12;
	R[2][0] = (float)r20;
	R[2][1] = (float)r21;
	R[2][2] = (float)r22;

	R[0][3] = (float)v0;
	R[1][3] = (float)v1;
	R[2][3] = (float)v2;
	

	//SbRotation tempR;
//	r1.setValue((const float*)R);
	//tempR.setValue(R.transpose());
	_transform->setMatrix(R.transpose());
	_rotMatrix = new SbMatrix[1];
	_rotMatrix[0]=R.transpose();
	//_transform->rotation.setValue(R.transpose());
}

void libCoin3D::Transform::invert()
{
	_transform->setMatrix(_rotMatrix[0].inverse());
}

bool libCoin3D::Transform::isIdentity()
{
	SbMatrix ident = SbMatrix::identity();
	return ident.equals(_rotMatrix[0],0.001f) != 0;
}

SoNode* libCoin3D::Transform::getNode()
{
	return _transform;
}