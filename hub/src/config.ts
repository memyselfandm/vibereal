export const config = {
  port: parseInt(process.env.PORT || "8080", 10),
  apiKey: process.env.HUB_API_KEY || "",
};
