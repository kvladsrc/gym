#include <algorithm>
#include <climits>
#include <cmath>
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

int main(int argc, char *argv[]) {
  int64_t k;
  std::cin >> k;
  int cf_size = string("codeforces").size();

  int64_t contain = 1;

  vector<int> res(10, 1);
  int idx = 0;
  while (contain < k) {
    res[idx]++;
    idx = (idx + 1) % cf_size;

    contain = 1;
    for (auto i : res) {
      contain *= i;
    }
  }

  string out;
  for (int l = 0; l < cf_size; ++l) {
    for (int i = 0; i < res[l]; ++i) {
      out += string("codeforces")[l];
    }
  }
  std::cout << out << "\n";

  return 0;
}
