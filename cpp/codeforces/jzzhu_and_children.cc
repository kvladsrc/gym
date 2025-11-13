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
  int n, m;
  std::cin >> n >> m;

  int last_iter = 0;
  int last_pos = 0;
  for (int i = 1; i <= n; ++i) {
    int buf;
    std::cin >> buf;

    int buf_iters = buf / m + ((buf % m) ? 1 : 0);
    if (buf_iters >= last_iter) {
      last_iter = buf_iters;
      last_pos = i;
    }
  }
  std::cout << last_pos << "\n";

  return 0;
}
