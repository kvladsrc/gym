#include <iostream>

// O(1)
bool watermelon(int n) { return !(n % 2) && n > 2; }

int main() {
  int n;
  std::cin >> n;
  if (watermelon(n))
    std::cout << "YES"
              << "\n";
  else
    std::cout << "NO"
              << "\n";
}
