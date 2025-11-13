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
  int n, m, k;
  std::cin >> n >> m >> k;

  vector<int> armies(m + 1);
  for (int i = 0; i <= m; ++i) {
    std::cin >> armies[i];
  }

  int res = 0;
  for (int i = 0; i < m; ++i) {
    int xor_value = armies[i] ^ armies[m];
    int count = 0;
    for (int j = 0; j < 32; ++j) {
      if (xor_value & (1U << j)) {
        ++count;
      }
    }
    if (count <= k) {
      ++res;
    }
  }

  std::cout << res << std::endl;

  return 0;
}
