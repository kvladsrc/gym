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

// Return true if a * m = want, where m positive integer.
bool compiant(int a, int want) {
  if (a == 0 || want == 0) {
    return a == want;
  }

  if (want % a != 0) {
    return false;
  }

  return (a < 0) == (want < 0);
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int a, b, c;
    std::cin >> a >> b >> c;
    int want;

    // Multiply c
    want = (b + (b - a));
    if (compiant(c, want)) {
      std::cout << "YES"
                << "\n";
      continue;
    }

    // Multiply a
    want = (b - (c - b));
    if (compiant(a, want)) {
      std::cout << "YES"
                << "\n";
      continue;
    }

    // Multiply b
    want = (a + (c - a) / 2);
    if (compiant(b, want) && (c - a) % 2 == 0) {
      std::cout << "YES"
                << "\n";
      continue;
    }

    std::cout << "NO"
              << "\n";
  }

  return 0;
}
