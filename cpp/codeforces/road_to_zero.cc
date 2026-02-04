#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
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
    int64_t x, y;
    std::cin >> x >> y;

    int64_t a, b;
    std::cin >> a >> b;

    int64_t cost = 0;
    if (b >= 2 * a) {
      cost += abs(x * a);
      cost += abs(y * a);
    } else {
      cost += min(x, y) * b;
      cost += (max(x, y) - min(x, y)) * a;
    }

    std::cout << cost << "\n";
  }
  return 0;
}
