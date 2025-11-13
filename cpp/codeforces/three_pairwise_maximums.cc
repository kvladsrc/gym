#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int x, y, z;
    std::cin >> x >> y >> z;

    if (x != y && x != z && y != z) {
      std::cout << "NO"
                << "\n";
      continue;
    }

    if (x == y && x >= z) {
      std::cout << "YES"
                << "\n";
      std::cout << x << " " << 1 << " " << z << "\n";
      continue;
    }

    if (x == z && x >= y) {
      std::cout << "YES"
                << "\n";
      std::cout << x << " " << 1 << " " << y << "\n";
      continue;
    }

    if (y == z && y >= x) {
      std::cout << "YES"
                << "\n";
      std::cout << y << " " << 1 << " " << x << "\n";
      continue;
    }

    // a, a, b and b > a
    std::cout << "NO"
              << "\n";
  }

  return 0;
}
