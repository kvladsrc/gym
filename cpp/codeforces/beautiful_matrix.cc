#include <iostream>

int abs(int a) {
  if (a < 0) return -a;
  return a;
}

// O(1)
int actions(int x, int y) { return abs(2 - x) + abs(2 - y); }

// O(n^2)
int main() {
  int x = 0, y = 0;
  bool found = false;
  for (int i = 0; i < 5; ++i) {
    for (int j = 0; j < 5; ++j) {
      int in;
      std::cin >> in;
      if (in) {
        x = j;
        y = i;
        found = true;
        break;
      }
    }
    if (found) break;
  }

  std::cout << actions(x, y) << "\n";
}
