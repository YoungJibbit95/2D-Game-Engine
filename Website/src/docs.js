import { mount } from "svelte";
import DocsApp from "./DocsApp.svelte";
import "./styles/app.css";

mount(DocsApp, { target: document.querySelector("#app") });
