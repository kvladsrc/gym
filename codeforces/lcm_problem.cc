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

int64_t lcm(int64_t a, int64_t b) {
  int64_t d = gcd(a, b);
  return a * b / d;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int64_t l, r;
    std::cin >> l >> r;
    if (2 * l > r) {
      std::cout << "-1 -1"
                << "\n";
    } else {
      std::cout << l << " " << 2 * l << "\n";
    }
  }
  return 0;
}
