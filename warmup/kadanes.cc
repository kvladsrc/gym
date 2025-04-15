#include <algorithm>
#include <iostream>
#include <vector>

using std::max;
using std::vector;

namespace {

/*
 * Kadane's Algorithm <3.
 */
int64_t kadanes(vector<int64_t> const &a) {
  int64_t global = 0;
  int64_t local = 0;
  for (auto i : a) {
    local = max(i, i + local);
    global = max(global, local);
  }
  return global;
}

}  // namespace

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;
  vector<int64_t> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }
  std::cout << kadanes(a) << "\n";
  return 0;
}
