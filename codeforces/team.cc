#include <iostream>
#include <vector>

using std::vector;

// O(n)
int solved(const vector<int> &first, const vector<int> &second,
           const vector<int> &third) {
  int res = 0;
  for (size_t i = 0; i < first.size(); ++i) {
    int count = first[i] + second[i] + third[i];
    if (count < 2) continue;
    res++;
  }
  return res;
}

int main() {
  int n;
  std::cin >> n;
  vector<int> first(n), second(n), third(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> first[i];
    std::cin >> second[i];
    std::cin >> third[i];
  }
  std::cout << solved(first, second, third) << "\n";
}
