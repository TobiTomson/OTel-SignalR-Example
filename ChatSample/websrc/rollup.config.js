import resolve from "@rollup/plugin-node-resolve";
import copy from "rollup-plugin-copy";
import { terser } from "rollup-plugin-terser";
import commonjs from 'rollup-plugin-commonjs';

export default {
    input: ["main.js"],
    output: [
        {
            dir: "../wwwroot/lib",
            format: "iife",
            sourcemap: false,
        },
    ],
    plugins: [
        commonjs(),
        resolve({
            mainFields: ["browser:module", "module", "main", "browser"],
        }),
        terser(),
        copy({
            targets: [
                {
                    src: ["index.html"],
                    dest: "../wwwroot",
                },
            ],
        }),
    ],
};