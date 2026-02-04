#include <algorithm>
#include <cstdint>
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
    int64_t n, m, x;
    std::cin >> n >> m >> x;

    int64_t row, col;
    row = x % n;
    if (!row) row = n;
    col = x / n + (row == n ? 0 : 1);

    std::cout << (row - 1) * m + col << "\n";
  }
  return 0;
}
