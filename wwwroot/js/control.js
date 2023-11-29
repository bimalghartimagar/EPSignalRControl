import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';


// Connect to the SensorDataHub (your SignalR hub for sensor data)
const controlHubConnection = new signalR.HubConnectionBuilder()
    .withUrl("/controlhub") // Replace with the actual URL of your SensorDataHub
    .build();








let scene, camera, renderer, boxModel;
// Define variables
const boxRotationSpeed = 0.01; // You can adjust the rotation speed

// Load 3D Scene
scene = new THREE.Scene();

// Load Camera Perspektive
camera = new THREE.PerspectiveCamera(50, window.innerWidth / window.innerHeight, 1, 200);
camera.position.set(0, 0, 0);

// Load a Renderer
renderer = new THREE.WebGLRenderer({ alpha: false });
renderer.setClearColor(0xC5C5C3);
renderer.setPixelRatio(window.devicePixelRatio);
renderer.setSize(400, 300);
document.getElementById("wrapper").appendChild(renderer.domElement);

// Load the Orbitcontroller
var controls = new OrbitControls(camera, renderer.domElement);

// Load Light
var ambientLight = new THREE.AmbientLight(0xcccccc);
scene.add(ambientLight);

var directionalLight = new THREE.DirectionalLight(0xffffff);
directionalLight.position.set(0, 1, 1).normalize();
scene.add(directionalLight);
// Load the phone model (you'll need to have a 3D model file)
const loader = new GLTFLoader();
loader.load('https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Models/master/2.0/Box/glTF/Box.gltf', function (gltf) {
    boxModel = gltf.scene;
    gltf.scene.scale.set(2, 2, 2);
    gltf.scene.position.x = 0;				    //Position (x = right+ left-) 
    gltf.scene.position.y = 0;				    //Position (y = up+, down-)
    gltf.scene.position.z = 0;				    //Position (z = front +, back-)
    scene.add(boxModel);
});

// Handle device orientation events
function handleOrientation(event) {
    const alpha = event.alpha;
    const beta = event.beta;
    const gamma = event.gamma;

    if (boxModel) {
        // Apply rotation based on the device's gyroscope data
        boxModel.rotation.x = beta * (Math.PI / 180);
        boxModel.rotation.y = gamma * (Math.PI / 180);
        boxModel.rotation.z = alpha * (Math.PI / 180);
    }
}

// Add an event listener for device orientation
window.addEventListener('deviceorientation', handleOrientation);

// Position the camera
camera.position.z = 5;

// Create an animation loop to continuously render the scene
function animate() {
    requestAnimationFrame(animate);

    // if (boxModel) {
    //     // Rotate the phone model continuously
    //     boxModel.rotation.x += boxRotationSpeed;
    //     boxModel.rotation.y += boxRotationSpeed;
    // }

    renderer.render(scene, camera);
}

animate();
















// Handle receiving sensor data and moving the 3D object
controlHubConnection.on("ReceiveSensorData", function (sensorData) {
    // Extract data (e.g., beta) from the sensor data received
    const beta = sensorData.beta;

    // Update the position of the 3D object based on the sensor data
    cube.rotation.x = beta * (Math.PI / 180);

    // Render the updated scene
    renderer.render(scene, camera);
});

// Start the SignalR connection
controlHubConnection.start().then(function () {
    console.log("Connected to SensorDataHub");
}).catch(function (err) {
    console.error("Error connecting to SensorDataHub: " + err);
});