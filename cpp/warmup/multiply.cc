#include <iostream>
#include <string>

using std::string;

static int multiply(int a, int b) {
  if (a == 1) {
    return b;
  }

  if (a % 2 == 0) {
    return multiply(a / 2, b * 2);
  }

  return b + multiply(a / 2, b * 2);
}

int main(int /*argc*/, char* /*argv*/[]) {
  int a = 0;
  int b = 0;
  std::cin >> a >> b;
  std::cout << multiply(a, b) << "\n";
  return 0;
}
