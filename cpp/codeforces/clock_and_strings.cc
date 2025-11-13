#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <queue>
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
using std::priority_queue;
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
    int a, b, c, d;
    std::cin >> a >> b >> c >> d;

    b = (b + 12 - a) % 12;
    c = (c + 12 - a) % 12;
    d = (d + 12 - a) % 12;

    if (b < min(c, d) || b > max(c, d)) {
      std::cout << "NO"
                << "\n";
    } else {
      std::cout << "YES"
                << "\n";
    }
  }

  return 0;
}
