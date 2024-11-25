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

  int result = 0;
  for (int c : all) {
    if (c && c >= all[k - 1]) {
      result++;
    }
  }

  std::cout << result << "\n";

  return 0;
}
