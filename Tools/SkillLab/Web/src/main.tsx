import { createRoot } from 'react-dom/client';
import App from './App';
import '@xyflow/react/dist/style.css';
import './style.css';
import './authoring.css';
import './runtime.css';
import './combo.css';
import './workbench.css';
createRoot(document.getElementById('root')!).render(<App/>);
