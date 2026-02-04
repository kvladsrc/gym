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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int a, b, c;
    std::cin >> a >> b >> c;

    int half = max(a, b) - min(a, b);
    if (c > half * 2 || a > half * 2 || b > half * 2) {
      std::cout << -1 << "\n";
    } else {
      int c_opposite = (c + half) % (half * 2);
      if (!c_opposite) {
        c_opposite = half * 2;
      }

      std::cout << c_opposite << "\n";
    }
  }

  return 0;
}
