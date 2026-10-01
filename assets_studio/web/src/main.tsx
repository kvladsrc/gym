import "./styles.css";

import { render } from "preact";

import { App } from "./App";
import { connect } from "./state/store";

connect();
const root = document.getElementById("app");
if (root) render(<App />, root);
