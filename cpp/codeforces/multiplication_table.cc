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
  int64_t n, x;
  std::cin >> n >> x;
  if (x > n * n) {
    std::cout << 0 << "\n";
    return 0;
  }

  int cell_nums = 0;
  for (int64_t i = 1; i * i <= x; ++i) {
    if (x % i == 0) {
      int64_t i_2 = x / i;
      if (i_2 > n) continue;

      if (i_2 == i) {
        cell_nums++;
      } else {
        cell_nums += 2;
      }
    }
  }

  std::cout << cell_nums << "\n";

  return 0;
}
