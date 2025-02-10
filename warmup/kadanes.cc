#include <algorithm>
#include <climits>
#include <cstddef>
#include <cstdint>
#include <iostream>
#include <vector>

using std::max;
using std::vector;

// Kadane's Algorithm <3.
static int64_t kadanes(vector<int> &a) {
  int64_t global = 0;
  int64_t local = 0;

  for (auto i : a) {
    auto i_casted = static_cast<int64_t>(i);
    local = max(i_casted, i_casted + local);
    global = max(global, local);
  }

  return global;
}

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  std::cout << kadanes(a) << "\n";

  return 0;
}
