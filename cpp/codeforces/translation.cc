#include <cstddef>
#include <iostream>
#include <string>

using std::string;

int main(int argc, char *argv[]) {
  string a, b;
  std::cin >> a >> b;
  int reversed = true;
  for (size_t i = 0; i < a.size(); ++i) {
    if (a[i] != b[(b.size() - 1) - i]) {
      reversed = false;
      break;
    }
  }
  if (reversed) {
    std::cout << "YES"
              << "\n";
  } else {
    std::cout << "NO"
              << "\n";
  }

  return 0;
}
