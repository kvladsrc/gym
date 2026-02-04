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

struct point {
  int x, y;
};

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    point a, b, f;
    std::cin >> a.x >> a.y;
    std::cin >> b.x >> b.y;
    std::cin >> f.x >> f.y;

    int res;
    res = abs(a.x - b.x) + abs(a.y - b.y);

    if (a.x == b.x && a.y != b.y && f.x == a.x) {
      if ((f.y >= a.y && f.y <= b.y) || (f.y <= a.y && f.y >= b.y)) res += 2;
    }

    if (a.y == b.y && a.x != b.x && f.y == a.y) {
      if ((f.x >= a.x && f.x <= b.x) || (f.x <= a.x && f.x >= b.x)) res += 2;
    }

    std::cout << res << "\n";
  }

  return 0;
}
