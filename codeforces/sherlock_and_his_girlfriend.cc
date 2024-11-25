#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

void fill_erato(vector<bool> &a) {
  for (size_t i = 0; i < a.size(); ++i) {
    if (!a[i])
      continue;
    for (size_t j = 2; (((i + 2) * j) - 2) < a.size(); ++j) {
      a[(((i + 2) * j) - 2)] = false;
    }
  }
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;

  vector<bool> erato(n, true);
  fill_erato(erato);

  if (n > 2) {
    std::cout << 2 << "\n";
  } else {
    std::cout << 1 << "\n";
  }

  for (auto b : erato) {
    if (b) {
      std::cout << 1 << " ";
    } else {
      std::cout << 2 << " ";
    }
  }
  std::cout << "\n";

  return 0;
}
