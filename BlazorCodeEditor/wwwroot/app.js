export function captureConsoleOutput(dotnetHelper) {
    // Save the original console methods
    const originalLog = console.log;
    const originalError = console.error;
    const originalWarn = console.warn;

    console.log = function (...args) {
        const message = args.map(arg => (typeof arg === 'object' ? JSON.stringify(arg) : arg)).join(' ');
        dotnetHelper.invokeMethodAsync("OnConsoleLog", message);
        originalLog.apply(console, args); 
    };

    console.error = function (...args) {
        const message = args.map(arg => (typeof arg === 'object' ? JSON.stringify(arg) : arg)).join(' ');
        dotnetHelper.invokeMethodAsync("OnConsoleError", message);
        originalError.apply(console, args);
    };

    console.warn = function (...args) {
        const message = args.map(arg => (typeof arg === 'object' ? JSON.stringify(arg) : arg)).join(' ');
        dotnetHelper.invokeMethodAsync("OnConsoleWarn", message);
        originalWarn.apply(console, args);
    };
}