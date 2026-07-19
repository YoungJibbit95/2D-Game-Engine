import { mount } from "svelte";
import HomeApp from "./HomeApp.svelte";
import "./styles/app.css";

mount(HomeApp, { target: document.querySelector("#app") });
