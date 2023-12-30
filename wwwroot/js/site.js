import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

let controlRequested = false;
let controlGranted = false;
let destroyController = false;

const requestCtrlBtn = document.getElementById('requestCtrlBtn')
requestCtrlBtn.disabled = true;
requestCtrlBtn.innerText = "Request Control";

const messageCtrl = document.getElementById('message')

// Connect to the SensorDataHub (your SignalR hub for sensor data)
const controlHubConnection = new signalR.HubConnectionBuilder()
    .withUrl("/controlhub") // Replace with the actual URL of your SensorDataHub
    .build();



requestCtrlBtn.addEventListener('click', () => {
    let action = 'RequestControl';
    if (controlGranted) {
        action = 'ReleaseControl';
    }
    controlHubConnection.invoke(action).catch(function (err) {
        return console.error(err.toString());
    });
})

controlHubConnection.on("ControlGranted", function () {
    controlGranted = true;
    controlRequested = false;
    requestCtrlBtn.disabled = false;
    requestCtrlBtn.classList.remove(...['btn-primary', 'btn-warning']);
    requestCtrlBtn.classList.add('btn-success');
    requestCtrlBtn.innerText = "Release Control";
    destroyController = createController();
});

controlHubConnection.on("ControlQueued", function () {
    controlRequested = true;
    controlGranted = false;
    requestCtrlBtn.disabled = true;
    requestCtrlBtn.classList.remove('btn-primary');
    requestCtrlBtn.classList.add('btn-warning');
    requestCtrlBtn.innerText = "Waiting in Queue";
});

controlHubConnection.on("ControlReleased", function () {
    controlRequested = false;
    controlGranted = false;
    requestCtrlBtn.disabled = false;
    requestCtrlBtn.innerText = "Request Control";
    requestCtrlBtn.classList.remove(...['btn-success', 'btn-warning']);
    requestCtrlBtn.classList.add('btn-primary');
    messageCtrl.innerText = '';
    messageCtrl.classList.add('d-none');

    if (destroyController != null) {
        destroyController();
        destroyController = null;
    }
});

controlHubConnection.on("ControlRemaining", function (seconds) {
    messageCtrl.classList.remove('d-none');
    messageCtrl.innerText = `Control Granted: ${seconds} seconds remaining`;
});


function createRenderer(canvsDOMId) {
    // Load a Renderer
    let renderer = new THREE.WebGLRenderer({ alpha: false, antialias: true });
    renderer.setClearColor(0xC5C5C3);
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.setSize(250, 250);
    document.getElementById(canvsDOMId).appendChild(renderer.domElement);

    return renderer;
}

function createScene() {
    // Load 3D Scene
    let scene = new THREE.Scene();

    // Load Light
    var ambientLight = new THREE.AmbientLight(0xcccccc);
    scene.add(ambientLight);

    var directionalLight = new THREE.DirectionalLight(0xffffff);
    directionalLight.position.set(0, 0, 1).normalize();
    scene.add(directionalLight);

    return scene;
}

function createCamera(cameraZPosition = 10) {

    // Load Camera Perspektive
    let camera = new THREE.PerspectiveCamera(50, 250 / 250, 1, 200);
    camera.position.z = 5;

    // Load the Orbitcontroller
    return camera;
}

function createCube() {
    const geometry = new THREE.BoxGeometry(2, 2, 2).toNonIndexed();;

    const positionAttribute = geometry.getAttribute('position');
    const colors = [];
    const color = new THREE.Color();

    for (let i = 0; i < positionAttribute.count; i += 3) {

        if (i >= 0 && i <= 9) {
            color.set(0xffff00); // x facing yellow
        }
        else if (i >= 10 && i <= 21) {
            color.set(0xff0000); // y facing red
        }
        else {
            color.set(0x0000ff); // z facing blue
        }

        // define the same color for each vertex of a triangle

        colors.push(color.r, color.g, color.b);
        colors.push(color.r, color.g, color.b);
        colors.push(color.r, color.g, color.b);

    }
    geometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
    const material = new THREE.MeshBasicMaterial({ vertexColors: true });
    let cube = new THREE.Mesh(geometry, material);

    // wireframe
    var geo = new THREE.EdgesGeometry(cube.geometry); // or WireframeGeometry
    var mat = new THREE.LineBasicMaterial({ color: 0xffffff });
    var wireframe = new THREE.LineSegments(geo, mat);
    cube.add(wireframe);

    return cube;
}



// Define variables
const boxRotationSpeed = 0.01; // You can adjust the rotation speed

const receiverRenderer = createRenderer('receiver-wrapper');
const receiverScene = createScene();
const receiverCamera = createCamera();
const receiverCube = createCube();
receiverScene.add(receiverCube);

let receiverControls = new OrbitControls(receiverCamera, receiverRenderer.domElement);
receiverControls.enabled = false;
function animate() {
    requestAnimationFrame(animate);
    receiverControls.update();
    receiverRenderer.render(receiverScene, receiverCamera);
}
animate();


controlHubConnection.on("ReceiveData", function (cameraData) {
    receiverCamera.position.x = cameraData.x;
    receiverCamera.position.y = cameraData.y;
    receiverCamera.position.z = cameraData.z;
});


function createController() {
    const controllerRenderer = createRenderer('controller-wrapper');
    const controllerScene = createScene();
    const controllerCamera = createCamera();
    const controllerCube = createCube();
    controllerScene.add(controllerCube);

    let controls = new OrbitControls(controllerCamera, controllerRenderer.domElement);
    function onPositionChange(o) {
        controlHubConnection.invoke("SendData", controllerCamera.position).catch(function (err) {
            return console.error(err.toString());
        });
    }
    controls.addEventListener('change', onPositionChange);

    let request;
    function controllerAnimate() {
        request = requestAnimationFrame(controllerAnimate);
        controls.update();
        controllerRenderer.render(controllerScene, controllerCamera);
    }

    controllerAnimate();

    return function () {
        document.getElementById('controller-wrapper').innerHTML = '';
        cancelAnimationFrame(request);
        controls.removeEventListener('change', onPositionChange);
    }
}







// Handle device orientation events
//function handleDeviceOrientation(event) {
//    const alpha = event.alpha;
//    const beta = event.beta;
//    const gamma = event.gamma;

//    if (cube) {
//        // Apply rotation based on the device's gyroscope data
//        cube.rotation.x = beta * (Math.PI / 180);
//        cube.rotation.y = gamma * (Math.PI / 180);
//        cube.rotation.z = alpha * (Math.PI / 180);
//    }
//    document.getElementById('xyz').innerHTML = `alpha:${alpha}<br> beta:${beta}<br>gamma:${gamma}<br> cube.rotation.z:${cube.rotation.z}<br> cube.rotation.x:${cube.rotation.x}<br> cube.rotation.y:${cube.rotation.x} `
//}

//function handleDeviceMotion(event) {
//    if (cube) {
//        // Apply rotation based on the device's gyroscope data
//        cube.rotation.x = Math.round(event.accelerationIncludingGravity.x * 5) / 25;
//        cube.rotation.y = Math.round(event.accelerationIncludingGravity.y * 5) / 25;
//        cube.rotation.z = Math.round(event.accelerationIncludingGravity.z * 5) / 5;
//    }
//    document.getElementById('xyz').innerHTML = `alpha:${alpha}<br> beta:${beta}<br>gamma:${gamma}<br> cube.rotation.z:${cube.rotation.z}<br> cube.rotation.x:${cube.rotation.x}<br> cube.rotation.y:${cube.rotation.x} `
//}

//var throttledHandling = _.throttle(handleDeviceMotion, 100);


// Add an event listener for device orientation
//window.addEventListener('deviceorientation', throttledHandling);
//window.addEventListener('devicemotion', throttledHandling);


// Create an animation loop to continuously render the scene





// Handle receiving sensor data and moving the 3D object


// Start the SignalR connection
controlHubConnection.start().then(function () {
    console.log("Connected to SensorDataHub");
    requestCtrlBtn.disabled = false;
}).catch(function (err) {
    console.error("Error connecting to SensorDataHub: " + err);
});