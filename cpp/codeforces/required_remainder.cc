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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    // int64_t res;
    // res % x = y, 0 <= res <= n
    // res = a * x + y;
    int64_t x, y, n;
    std::cin >> x >> y >> n;
    int64_t a = (n - y) / x;
    std::cout << a * x + y << "\n";
  }

  return 0;
}
