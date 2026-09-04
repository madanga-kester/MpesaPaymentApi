## M-Pesa Payment API

The M-Pesa Payment API is the underlying payment-processing backend that serves as the target system for the **M-Pesa AI Security Copilot** project.

It is a backend API designed to model common M-Pesa-style payment operations, including transaction processing, payment requests, transaction validation, and related API workflows. The system provides a realistic software environment in which API security, authentication, authorization, input validation, business-logic vulnerabilities, and other security concerns can be explored.

The API is intentionally used as the **system under analysis** rather than embedding security logic directly into the AI layer. This separation allows the project to explore how an AI security copilot can observe, analyze, and reason about an existing payment API.

### Purpose

The M-Pesa API provides the application layer that the AI Security Copilot will eventually analyze and interact with.

The project focuses on identifying and understanding security risks such as:

* Authentication and authorization weaknesses
* Insecure API endpoints
* Input validation vulnerabilities
* Broken access control
* Business-logic flaws
* Improper error handling
* Sensitive data exposure
* API abuse and anomalous requests
* Potential injection attacks
* Security misconfigurations

### Architecture

The intended architecture separates the payment API from the AI security layer:



The goal is to evolve this architecture from a conventional payment API into an environment where AI can assist developers and security engineers in understanding, detecting, and responding to security threats.

### Technology

The API is being developed as a modern backend service using **ASP.NET Core / C#**, with a relational database for persistence.

The implementation is intentionally kept close to a conventional production-style API so that the security challenges encountered by the AI copilot are representative of real software systems.

### Relationship to the AI Security Copilot

The M-Pesa API is the **target application** of this project.

The AI Security Copilot is being developed separately as an intelligence layer capable of eventually:

1. Understanding the API's architecture and behavior.
2. Analyze API requests and responses.
3. Identify potential security vulnerabilities.
4. Explain why a behavior may be dangerous.
5. Recommend defensive actions.
6. Use security tools to investigate suspicious activity.
7. Retrieve relevant security knowledge through RAG.
8. Assist developers in securing the underlying payment system.

This separation is intentional: the objective is not simply to build an AI chatbot, but to explore how AI can be engineered into a practical security system around a real software application.
