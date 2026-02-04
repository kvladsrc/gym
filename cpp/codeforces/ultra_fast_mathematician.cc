#include <cstddef>
#include <iostream>
#include <string>

using std::string;

int main(int argc, char* argv[]) {
  string a, b;
  std::cin >> a >> b;
  for (size_t i = 0; i < a.size(); ++i) {
    if ((a[i] - '0') + (b[i] - '0') == 1) {
      std::cout << 1;
    } else {
      std::cout << 0;
    }
  }
  std::cout << "\n";

  return 0;
}
