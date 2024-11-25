#include <algorithm>
#include <cstddef>
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

int64_t count(vector<int64_t> &in) {
  int64_t res = 0;
  unordered_map<int64_t, int64_t> m;
  for (size_t i = 0; i < in.size(); ++i) {
    res += m[in[i] - i];
    m[in[i] - i]++;
  }

  return res;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int64_t> in(n);
    for (int i = 0; i < n; ++i) {
      std::cin >> in[i];
    }

    std::cout << count(in) << "\n";
  }
  return 0;
}
