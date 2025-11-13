#include <algorithm>
#include <iostream>
#include <vector>

using std::vector;

// O(n)
int main(int argc, char *argv[]) {
  int n, k;
  std::cin >> n >> k;

  vector<int> all(n);

  for (int i = 0; i < n; ++i) {
    std::cin >> all[i];
  }

  int result = std::count_if(all.begin(), all.end(),
                             [&all, k](int c) { return c && c >= all[k - 1]; });

  std::cout << result << "\n";

  return 0;
}
